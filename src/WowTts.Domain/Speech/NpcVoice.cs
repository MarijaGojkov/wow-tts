namespace WowTts.Domain.Speech;

// The voice cast for an NPC. Every quest that NPC gives uses it, so they sound the same each time.
public sealed record NpcVoice(string NpcName, string NpcGender, string Voice, string Reason);
