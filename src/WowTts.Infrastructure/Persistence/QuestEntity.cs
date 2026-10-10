using WowTts.Domain.Maps;
using WowTts.Domain.Quests;

namespace WowTts.Infrastructure.Persistence;

// One row of the Quests table. Kept separate from the Domain's Quest, so database details
// (settable properties, UpdatedAt) don't leak into the Domain.
public sealed class QuestEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string NpcName { get; set; } = "";
    public string NpcRace { get; set; } = "";
    public string NpcGender { get; set; } = "";
    public string? AcceptText { get; set; }
    public string? ProgressText { get; set; }
    public string? CompleteText { get; set; }
    public QuestSource Source { get; set; }
    public int? Level { get; set; }
    public int? MinLevel { get; set; }
    public QuestSide Side { get; set; }
    public int? CategoryId { get; set; }

    // Where the quest giver stands in the world, if known.
    public int? GiverMapId { get; set; }
    public double? GiverX { get; set; }
    public double? GiverY { get; set; }

    // The NPC you hand the quest in to, when it isn't the quest giver.
    public string? EnderName { get; set; }
    public string? EnderRace { get; set; }
    public string? EnderGender { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Quest ToDomain() => new(
        Id, Title, NpcName, NpcRace, NpcGender, AcceptText, ProgressText, CompleteText, Source,
        Level, MinLevel, Side, CategoryId,
        GiverMapId is int mapId && GiverX is double x && GiverY is double y ? new WorldLocation(mapId, x, y) : null,
        EnderName is string name ? new QuestNpc(name, EnderRace ?? "Unknown", EnderGender ?? "Unknown") : null);

    public void CopyFrom(Quest quest)
    {
        Id = quest.Id;
        Title = quest.Title;
        NpcName = quest.NpcName;
        NpcRace = quest.NpcRace;
        NpcGender = quest.NpcGender;
        AcceptText = quest.AcceptText;
        ProgressText = quest.ProgressText;
        CompleteText = quest.CompleteText;
        Source = quest.Source;
        Level = quest.Level;
        MinLevel = quest.MinLevel;
        Side = quest.Side;
        CategoryId = quest.CategoryId;
        GiverMapId = quest.GiverLocation?.MapId;
        GiverX = quest.GiverLocation?.X;
        GiverY = quest.GiverLocation?.Y;
        EnderName = quest.Ender?.Name;
        EnderRace = quest.Ender?.Race;
        EnderGender = quest.Ender?.Gender;
        UpdatedAt = DateTime.UtcNow;
    }
}
