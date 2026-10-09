using WowTts.Domain.Quests;

namespace WowTts.Application.Quests;

public enum AudioFilter
{
    All,
    Done,     // at least one part has audio
    Missing   // at least one part has no audio yet
}

// Filters and paging for GET /api/quests. Every filter is optional.
public sealed record QuestQuery
{
    // Matches the quest ID, title, quest giver or quest log header.
    public string? Search { get; init; }

    // A quest log header, e.g. 12 (Elwynn Forest) or -22 (Seasonal).
    public int? CategoryId { get; init; }

    // A map zone or continent: quests whose giver stands there.
    public int? MapZoneId { get; init; }

    public AudioFilter Audio { get; init; } = AudioFilter.All;

    public QuestSource? Source { get; init; }

    // Alliance or Horde also include quests both factions can take.
    public QuestSide? Side { get; init; }

    public int? MinLevel { get; init; }
    public int? MaxLevel { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
