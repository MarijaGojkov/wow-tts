using WowTts.Domain.Maps;
using WowTts.Domain.Quests;

namespace WowTts.Application.Quests;

// A quest with all its text, for GET /api/quests/{id}. Parts only lists the parts that have text.
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
    IReadOnlyList<QuestPartDto> Parts);

// One part of a quest. Text is what will be spoken, with placeholders already replaced.
// HasAudio tells whether the MP3 has been generated yet.
public sealed record QuestPartDto(QuestPart Part, string Text, bool HasAudio);

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
