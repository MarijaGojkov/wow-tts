namespace WowTts.Domain.Quests;

// Where a quest's text came from. A higher value wins: text captured in game replaces
// the vanilla text from cMaNGOS, which replaces the hand-written samples.
public enum QuestSource
{
    Sample = 0,
    CMangos = 1,
    Captured = 2
}
