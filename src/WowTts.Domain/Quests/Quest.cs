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
        string acceptText,
        string? progressText,
        string? completeText)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "A quest ID must be positive.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException($"Quest {id} has no title.", nameof(title));

        if (string.IsNullOrWhiteSpace(acceptText))
            throw new ArgumentException($"Quest {id} has no accept text.", nameof(acceptText));

        Id = id;
        Title = title;
        NpcName = npcName;
        NpcRace = npcRace;
        NpcGender = npcGender;
        AcceptText = acceptText;
        ProgressText = progressText;
        CompleteText = completeText;
    }

    public int Id { get; }
    public string Title { get; }
    public string NpcName { get; }

    // Stored now, used later to give each NPC a fitting voice.
    public string NpcRace { get; }
    public string NpcGender { get; }

    public string AcceptText { get; }
    public string? ProgressText { get; }
    public string? CompleteText { get; }

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
