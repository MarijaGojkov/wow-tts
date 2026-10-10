using WowTts.Domain.Maps;
using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Application.Quests;

// A quest with all its text, for GET /api/quests/{id}. Parts only lists the parts that have text.
// Npc* and Voice are the quest giver; Ender is the NPC you hand the quest in to, when that's someone else.
public sealed record QuestDto(
    int Id,
    string Title,
    string NpcName,
    string NpcRace,
    string NpcGender,
    QuestSource Source,
    int? Level,
    int? MinLevel,
    QuestSide Side,
    int? CategoryId,
    string? CategoryName,
    int? MapZoneId,
    string? MapZoneName,
    MapPoint? MapPosition,
    NpcVoice? Voice,
    QuestNpcDto? Ender,
    IReadOnlyList<QuestPartDto> Parts);

// An NPC of a quest and the voice cast for them (null until their first audio).
public sealed record QuestNpcDto(string Name, string Race, string Gender, NpcVoice? Voice);

// One part of a quest. Text is what will be spoken, with placeholders already replaced, and Speaker who says it.
// HasAudio tells whether the MP3 has been generated yet, and Performance how it was spoken
// (null for audio made before the voice director existed).
public sealed record QuestPartDto(QuestPart Part, string Text, string Speaker, bool HasAudio, VoicePerformance? Performance);

// A short quest for lists and the map, without the text.
public sealed record QuestSummaryDto(
    int Id,
    string Title,
    string NpcName,
    QuestSource Source,
    int? Level,
    int? MinLevel,
    QuestSide Side,
    int? CategoryId,
    string? CategoryName,
    int? MapZoneId,
    string? MapZoneName,
    MapPoint? MapPosition,
    IReadOnlyList<QuestPartStatusDto> Parts);

public sealed record QuestPartStatusDto(QuestPart Part, bool HasAudio);

// One page of search results. Total is how many quests match all filters.
public sealed record QuestSearchResult(IReadOnlyList<QuestSummaryDto> Items, int Total, int Page, int PageSize);

public sealed record QuestStatsDto(int Quests, int Parts, int PartsWithAudio);

// A quest giver's spot on a map (a continent or a zone), in percent of that map. Light enough to send thousands.
public sealed record MapMarkerDto(
    int QuestId,
    string Title,
    string NpcName,
    int? Level,
    QuestSide Side,
    int? MapZoneId,
    MapPoint Position,
    int Parts,
    int PartsWithAudio);

// A quest log header with how many quests it has, for the zone filter.
public sealed record QuestCategoryDto(int Id, string Name, bool IsZone, int QuestCount);
