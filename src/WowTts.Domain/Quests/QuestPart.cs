namespace WowTts.Domain.Quests;

// The three moments a quest giver speaks: when you take the quest, when you return
// before finishing it, and when you hand it in.
public enum QuestPart
{
    Accept,
    Progress,
    Complete
}
