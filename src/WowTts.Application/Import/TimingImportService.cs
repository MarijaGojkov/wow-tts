using Microsoft.Extensions.Logging;
using WowTts.Application.Abstractions;
using WowTts.Application.Exceptions;
using WowTts.Domain.Maps;
using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Application.Import;

// Parts whose words matched the text poorly, so their audio may say something else.
public sealed record PoorMatch(int QuestId, QuestPart Part, double Match);

// WithAudio: quest parts with audio (in the zone, when one is given). AlreadyTimed: skipped, they have timings.
// Timed: given timings now.
// Failed: the recognizer couldn't read them. Stopped: true when cancelled before the end.
public sealed record TimingImportResult(
    int WithAudio, int AlreadyTimed, int Timed, int Failed, bool Stopped, IReadOnlyList<PoorMatch> PoorMatches);

// Gives audio that has no word timings yet (e.g. imported from AI VoiceOver) its timings, by letting a recognizer
// such as Whisper listen to it and lining the heard words up with the quest text. Each part is saved as soon as it's
// done, so a run can be stopped and started again.
public sealed class TimingImportService(
    IQuestRepository quests,
    IGameDataRepository gameData,
    IAudioStore audioStore,
    ITimingsRepository timings,
    ILogger<TimingImportService> logger)
{
    // Below this share of matched words, the audio probably doesn't say our text.
    public const double PoorMatchBelow = 0.7;

    // zone: only quests of that zone (e.g. "Teldrassil"), by quest log header or where the quest giver stands.
    public async Task<TimingImportResult> ImportAsync(
        IWordRecognizer recognizer, string? zone, bool redo, int? limit, Action<int, int>? progress, CancellationToken cancel)
    {
        Func<Quest, bool> inScope = zone is null ? _ => true : await InZoneAsync(zone);
        var questsById = (await quests.GetAllAsync()).Where(inScope).ToDictionary(quest => quest.Id);
        var withAudio = audioStore.ListAll()
            .Where(key => questsById.TryGetValue(key.QuestId, out var quest) && quest.HasText(key.Part))
            .ToList();
        var timed = redo ? new HashSet<AudioKey>() : await timings.ListAllAsync();

        var todo = withAudio.Where(key => !timed.Contains(key))
            .OrderBy(key => key.QuestId).ThenBy(key => key.Part)
            .Take(limit ?? int.MaxValue)
            .ToList();

        var poorMatches = new List<PoorMatch>();
        int done = 0, failed = 0;

        foreach (AudioKey key in todo)
        {
            if (cancel.IsCancellationRequested)
                break;

            try
            {
                byte[]? audio = await audioStore.TryReadAsync(key.QuestId, key.Part);
                if (audio is null)
                    continue;

                IReadOnlyList<HeardWord> heard = await recognizer.RecognizeAsync(audio, cancel);
                string text = TextCleaner.Clean(questsById[key.QuestId].GetText(key.Part)!);
                var (words, match) = WordAligner.Align(text, heard);

                if (words.Count == 0)
                {
                    failed++;
                    logger.LogWarning("Quest {QuestId} {Part}: no words were recognized", key.QuestId, key.Part);
                    continue;
                }

                await timings.SaveAsync(key.QuestId, key.Part, new AudioTimings(recognizer.Source, match, words));
                if (match < PoorMatchBelow)
                    poorMatches.Add(new PoorMatch(key.QuestId, key.Part, match));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(ex, "Quest {QuestId} {Part}: making timings failed", key.QuestId, key.Part);
            }

            progress?.Invoke(++done, todo.Count);
        }

        return new TimingImportResult(
            withAudio.Count, withAudio.Count(timed.Contains), done - failed, failed, cancel.IsCancellationRequested,
            poorMatches.OrderBy(p => p.Match).ToList());
    }

    // A quest is in the zone when its quest log header is that zone, or its quest giver stands there.
    // Cities are zones of their own: Darnassus isn't part of Teldrassil.
    private async Task<Func<Quest, bool>> InZoneAsync(string zone)
    {
        var categoryIds = (await gameData.GetCategoryNamesAsync())
            .Where(category => category.Value.Equals(zone, StringComparison.OrdinalIgnoreCase))
            .Select(category => category.Key)
            .ToHashSet();
        var mapZones = await gameData.GetMapZonesAsync();
        var zoneIds = mapZones
            .Where(mapZone => mapZone.Type == MapZoneType.Zone && mapZone.Name.Equals(zone, StringComparison.OrdinalIgnoreCase))
            .Select(mapZone => mapZone.Id)
            .ToHashSet();

        if (categoryIds.Count == 0 && zoneIds.Count == 0)
            throw new InvalidRequestException($"There's no zone called '{zone}'. Use its name as the viewer shows it, e.g. Teldrassil.");

        return quest =>
            (quest.CategoryId is int category && categoryIds.Contains(category))
            || (quest.GiverLocation is WorldLocation location && MapZone.FindZone(mapZones, location) is MapZone found && zoneIds.Contains(found.Id));
    }
}
