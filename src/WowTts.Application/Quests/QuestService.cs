using Microsoft.Extensions.Logging;
using WowTts.Application.Abstractions;
using WowTts.Application.Exceptions;
using WowTts.Application.Voices;
using WowTts.Domain.Maps;
using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Application.Quests;

public sealed class QuestService(
    IQuestRepository quests,
    IGameDataRepository gameData,
    IAudioStore audioStore,
    ISpeechSynthesizer synthesizer,
    VoiceCastingService voiceCasting,
    IPerformanceRepository performances,
    INpcVoiceRepository npcVoices,
    ITimingsRepository timings,
    ILogger<QuestService> logger)
{
    public const int MaxPageSize = 500;

    // One page of quests matching the query, ordered by level and then title.
    public async Task<QuestSearchResult> SearchAsync(QuestQuery query)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > MaxPageSize)
            throw new InvalidRequestException($"Page must be 1 or more, and page size between 1 and {MaxPageSize}.");

        Lookup lookup = await LoadLookupAsync();
        var all = await quests.GetAllAsync();

        var matches = all
            .Select(quest => (Quest: quest, Zone: lookup.ZoneOf(quest)))
            .Where(row => Matches(row.Quest, row.Zone, query, lookup))
            .OrderBy(row => row.Quest.Level ?? int.MaxValue)
            .ThenBy(row => row.Quest.Title)
            .ToList();

        var items = matches
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => ToSummary(row.Quest, row.Zone, lookup))
            .ToList();

        return new QuestSearchResult(items, matches.Count, query.Page, query.PageSize);
    }

    // Every matching quest giver on one map (query.MapZoneId), with their position on that map.
    // Uses the same filters as SearchAsync, but returns all matches without paging.
    public async Task<IReadOnlyList<MapMarkerDto>> GetMarkersAsync(QuestQuery query)
    {
        if (query.MapZoneId is not int mapId)
            throw new InvalidRequestException("Choose a map with mapZoneId, e.g. 1415 (Eastern Kingdoms) or 1429 (Elwynn Forest).");

        Lookup lookup = await LoadLookupAsync();
        MapZone map = lookup.Zones.FirstOrDefault(zone => zone.Id == mapId)
            ?? throw new NotFoundException($"There's no map with ID {mapId}.");

        var all = await quests.GetAllAsync();

        return all
            .Select(quest => (Quest: quest, Zone: lookup.ZoneOf(quest)))
            .Where(row => row.Quest.GiverLocation is WorldLocation location
                && map.Contains(location)
                && Matches(row.Quest, row.Zone, query, lookup))
            .Select(row =>
            {
                var parts = Enum.GetValues<QuestPart>().Where(row.Quest.HasText).ToList();
                return new MapMarkerDto(
                    row.Quest.Id, row.Quest.Title, row.Quest.NpcName, row.Quest.Level, row.Quest.Side,
                    row.Zone?.Id,
                    Round(map.ToMapPoint(row.Quest.GiverLocation!.Value)),
                    parts.Count,
                    parts.Count(part => lookup.SavedAudio.Contains(new AudioKey(row.Quest.Id, part))));
            })
            .ToList();
    }

    public async Task<QuestDto> GetAsync(int id)
    {
        Lookup lookup = await LoadLookupAsync();
        Quest quest = await GetQuestAsync(id);
        MapZone? zone = lookup.ZoneOf(quest);
        var performed = await performances.GetForQuestAsync(id);
        var timed = await timings.GetForQuestAsync(id);

        var parts = Enum.GetValues<QuestPart>()
            .Where(quest.HasText)
            .Select(part =>
            {
                bool hasAudio = lookup.SavedAudio.Contains(new AudioKey(quest.Id, part));
                return new QuestPartDto(
                    part,
                    TextCleaner.Clean(quest.GetText(part)!),
                    quest.SpeakerOf(part).Name,
                    hasAudio,
                    hasAudio ? performed.GetValueOrDefault(part) : null,
                    hasAudio ? timed.GetValueOrDefault(part) : null);
            })
            .ToList();

        return new QuestDto(
            quest.Id, quest.Title, quest.NpcName, quest.NpcRace, quest.NpcGender, quest.Source,
            quest.Level, quest.MinLevel, quest.Side,
            quest.CategoryId, lookup.CategoryName(quest.CategoryId),
            zone?.Id, zone?.Name, MapPosition(quest, zone),
            await npcVoices.GetAsync(quest.NpcName, quest.NpcGender),
            quest.Ender is QuestNpc ender
                ? new QuestNpcDto(ender.Name, ender.Race, ender.Gender, await npcVoices.GetAsync(ender.Name, ender.Gender))
                : null,
            parts);
    }

    public async Task<QuestStatsDto> GetStatsAsync()
    {
        var all = await quests.GetAllAsync();
        var saved = audioStore.ListAll();
        int parts = 0, withAudio = 0;

        foreach (Quest quest in all)
        {
            foreach (QuestPart part in Enum.GetValues<QuestPart>().Where(quest.HasText))
            {
                parts++;
                if (saved.Contains(new AudioKey(quest.Id, part)))
                    withAudio++;
            }
        }

        return new QuestStatsDto(all.Count, parts, withAudio);
    }

    // The quest log headers that have quests, zones first, each sorted by name.
    public async Task<IReadOnlyList<QuestCategoryDto>> GetCategoriesAsync()
    {
        Lookup lookup = await LoadLookupAsync();
        var all = await quests.GetAllAsync();

        return all
            .Where(quest => quest.CategoryId is not null)
            .GroupBy(quest => quest.CategoryId!.Value)
            .Select(group => new QuestCategoryDto(
                group.Key, lookup.CategoryName(group.Key) ?? $"Unknown ({group.Key})", group.Key > 0, group.Count()))
            .OrderByDescending(category => category.IsZone)
            .ThenBy(category => category.Name)
            .ToList();
    }

    // Returns the quest part's audio. It's generated with Azure the first time, then read from the saved file,
    // so each text is only paid for once.
    public async Task<byte[]> GetOrCreateAudioAsync(int id, QuestPart part)
    {
        if (!Enum.IsDefined(part))
            throw new InvalidRequestException($"Unknown quest part '{part}'. Use accept, progress or complete.");

        byte[]? saved = await audioStore.TryReadAsync(id, part);
        if (saved is not null)
        {
            logger.LogInformation("Quest {QuestId} {Part}: served saved audio", id, part);
            return saved;
        }

        Quest quest = await GetQuestAsync(id);

        string? text = quest.GetText(part);
        if (string.IsNullOrWhiteSpace(text))
            throw new NotFoundException($"Quest {id} has no {part.ToString().ToLowerInvariant()} text.");

        string spoken = TextCleaner.Clean(text);

        logger.LogInformation("Quest {QuestId} {Part}: generating audio for {Characters} characters",
            id, part, spoken.Length);

        // The voice director picks the voice and how the line is spoken.
        VoicePerformance performance = await voiceCasting.DecideAsync(quest, part, spoken);
        SpokenAudio result = await synthesizer.PerformAsync(spoken, performance);

        await audioStore.SaveAsync(id, part, result.Audio);
        await performances.SaveAsync(id, part, performance);

        // Azure reports each word as it speaks it, so the karaoke timings come for free.
        var (words, match) = WordAligner.Align(spoken, result.Words);
        if (words.Count > 0)
            await timings.SaveAsync(id, part, new AudioTimings(TimingSource.Azure, match, words));
        else
            await timings.DeleteAsync(id, part);

        return result.Audio;
    }

    // Removes a quest part's audio, so the next request generates it again with a fresh performance.
    public async Task DeleteAudioAsync(int id, QuestPart part)
    {
        if (!Enum.IsDefined(part))
            throw new InvalidRequestException($"Unknown quest part '{part}'. Use accept, progress or complete.");

        await GetQuestAsync(id);
        audioStore.Delete(id, part);
        await performances.DeleteAsync(id, part);
        await timings.DeleteAsync(id, part);
    }

    // Forgets the voice cast for one of this quest's NPCs, so the director casts a new one next time.
    // Audio that already exists keeps its old voice until it's regenerated.
    public async Task RecastAsync(int id, QuestNpcRole role)
    {
        Quest quest = await GetQuestAsync(id);
        QuestNpc npc = role == QuestNpcRole.Ender
            ? quest.Ender ?? throw new InvalidRequestException($"Quest {id} is handed in to its quest giver, so it has no other NPC to recast.")
            : quest.Giver;
        await npcVoices.DeleteAsync(npc.Name, npc.Gender);
    }

    private async Task<Quest> GetQuestAsync(int id)
    {
        return await quests.GetByIdAsync(id)
            ?? throw new NotFoundException($"There's no quest with ID {id}.");
    }

    private async Task<Lookup> LoadLookupAsync() => new(
        await gameData.GetMapZonesAsync(),
        await gameData.GetCategoryNamesAsync(),
        audioStore.ListAll());

    private static bool Matches(Quest quest, MapZone? zone, QuestQuery query, Lookup lookup)
    {
        if (query.CategoryId is int categoryId && quest.CategoryId != categoryId)
            return false;

        // A continent matches every quest whose giver stands in one of its zones.
        if (query.MapZoneId is int mapZoneId && zone?.Id != mapZoneId && zone?.ParentId != mapZoneId)
            return false;

        if (query.Source is QuestSource source && quest.Source != source)
            return false;

        if (query.Side is QuestSide side && side != QuestSide.Both && quest.Side != side && quest.Side != QuestSide.Both)
            return false;

        if (query.MinLevel is int minLevel && (quest.Level ?? 0) < minLevel)
            return false;

        if (query.MaxLevel is int maxLevel && (quest.Level ?? int.MaxValue) > maxLevel)
            return false;

        if (query.Audio != AudioFilter.All)
        {
            var parts = Enum.GetValues<QuestPart>().Where(quest.HasText).ToList();
            int withAudio = parts.Count(part => lookup.SavedAudio.Contains(new AudioKey(quest.Id, part)));

            if (query.Audio == AudioFilter.Done && withAudio == 0)
                return false;
            if (query.Audio == AudioFilter.Missing && withAudio == parts.Count)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            bool found = quest.Id.ToString() == term
                || quest.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || quest.NpcName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (quest.Ender?.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (lookup.CategoryName(quest.CategoryId)?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);

            if (!found)
                return false;
        }

        return true;
    }

    private static QuestSummaryDto ToSummary(Quest quest, MapZone? zone, Lookup lookup) => new(
        quest.Id, quest.Title, quest.NpcName, quest.Source, quest.Level, quest.MinLevel, quest.Side,
        quest.CategoryId, lookup.CategoryName(quest.CategoryId),
        zone?.Id, zone?.Name, MapPosition(quest, zone),
        Enum.GetValues<QuestPart>()
            .Where(quest.HasText)
            .Select(part => new QuestPartStatusDto(part, lookup.SavedAudio.Contains(new AudioKey(quest.Id, part))))
            .ToList());

    private static MapPoint? MapPosition(Quest quest, MapZone? zone) =>
        zone is not null && quest.GiverLocation is WorldLocation location
            ? Round(zone.ToMapPoint(location))
            : null;

    private static MapPoint Round(MapPoint point) => new(Math.Round(point.X, 1), Math.Round(point.Y, 1));

    // Game data and saved audio, loaded once per request.
    private sealed record Lookup(
        IReadOnlyList<MapZone> Zones,
        IReadOnlyDictionary<int, string> CategoryNames,
        IReadOnlySet<AudioKey> SavedAudio)
    {
        public MapZone? ZoneOf(Quest quest) =>
            quest.GiverLocation is WorldLocation location ? MapZone.FindZone(Zones, location) : null;

        public string? CategoryName(int? id) =>
            id is int key && CategoryNames.TryGetValue(key, out string? name) ? name : null;
    }
}
