namespace WowTts.Domain.Quests;

// Someone who speaks in a quest: the quest giver, or the NPC you hand the quest in to.
// Race is a playable race (Orc, NightElf…), a creature type for NPCs that don't look like one
// (Undead, Dragonkin, Elemental…), or Unknown.
public sealed record QuestNpc(string Name, string Race, string Gender)
{
    public bool IsSameNpc(QuestNpc other) => Name == other.Name && Gender == other.Gender;
}
