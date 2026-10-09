using WowTts.Domain.Maps;

namespace WowTts.Domain.Quests;

// A quest and what its quest giver says at each step.
public sealed class Quest
{
    public Quest(
        int id,
        string title,
        string npcName,
        string npcRace,
        string npcGender,
        string? acceptText,
        string? progressText,
        string? completeText,
        QuestSource source,
        int? level = null,
        int? minLevel = null,
        QuestSide side = QuestSide.Both,
        int? categoryId = null,
        WorldLocation? giverLocation = null)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "A quest ID must be positive.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException($"Quest {id} has no title.", nameof(title));

        // Some quests have no accept text, e.g. turn-in quests where the NPC only speaks when you hand them in.
        // But a quest with nothing to say at all is no use here.
        if (string.IsNullOrWhiteSpace(acceptText)
            && string.IsNullOrWhiteSpace(progressText)
            && string.IsNullOrWhiteSpace(completeText))
            throw new ArgumentException($"Quest {id} has no text.", nameof(acceptText));

        Id = id;
        Title = title;
        NpcName = npcName;
        NpcRace = npcRace;
        NpcGender = npcGender;
        AcceptText = acceptText;
        ProgressText = progressText;
        CompleteText = completeText;
        Source = source;
        Level = level;
        MinLevel = minLevel;
        Side = side;
        CategoryId = categoryId;
        GiverLocation = giverLocation;
    }

    public int Id { get; }
    public string Title { get; }
    public string NpcName { get; }

    // Stored now, used later to give each NPC a fitting voice.
    public string NpcRace { get; }
    public string NpcGender { get; }

    public string? AcceptText { get; }
    public string? ProgressText { get; }
    public string? CompleteText { get; }

    public QuestSource Source { get; }

    // The quest's level, and the level you need to take it. Null when unknown or when it scales with you.
    public int? Level { get; }
    public int? MinLevel { get; }

    public QuestSide Side { get; }

    // The quest log header the game files the quest under. A positive ID is a zone, city or dungeon
    // (e.g. 12 = Elwynn Forest); a negative ID is a category (e.g. -61 = Warlock, -22 = Seasonal).
    public int? CategoryId { get; }

    // Where the quest giver stands, if known.
    public WorldLocation? GiverLocation { get; }

    // The text for one part of the quest, or null when the quest has no text for that part.
    public string? GetText(QuestPart part) => part switch
    {
        QuestPart.Accept => AcceptText,
        QuestPart.Progress => ProgressText,
        QuestPart.Complete => CompleteText,
        _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Unknown quest part.")
    };

    public bool HasText(QuestPart part) => !string.IsNullOrWhiteSpace(GetText(part));
}
