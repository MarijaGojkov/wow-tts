namespace WowTts.Domain.Speech;

// How an NPC is played with gpt-4o-mini-tts: its voice (e.g. onyx) and a description of the character, such as
// "an old Ironforge dwarf with a thick Scottish brogue, gravelly and warm". Saved once, so the NPC always sounds
// the same; each line only adds how it's delivered.
public sealed record NpcCharacter(string NpcName, string NpcGender, string Voice, string Character, string Reason);
