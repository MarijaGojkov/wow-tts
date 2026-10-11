using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Application.Abstractions;

// A voice of an instruction-following speech model (gpt-4o-mini-tts), e.g. "onyx", and what it sounds like.
public sealed record CharacterVoice(string Name, string Description);

// Speech that follows written instructions: who the character is and how to say the line. Infrastructure provides
// gpt-4o-mini-tts on Azure OpenAI. IsAvailable is false until its deployment is set up.
public interface IInstructedSpeechSynthesizer
{
    string Name { get; }
    bool IsAvailable { get; }
    IReadOnlyList<CharacterVoice> Voices { get; }

    // Returns MP3 audio.
    Task<byte[]> SpeakAsync(string text, string voice, string instructions);
}

// What the character director knows about a line. Cast is the NPC's saved character, if they have one.
public sealed record CharacterDirectionRequest(
    string QuestTitle,
    QuestNpc Npc,
    QuestPart Part,
    string Text,
    IReadOnlyList<CharacterVoice> Voices,
    NpcCharacter? Cast);

// The director's answer: the voice and character (kept for the NPC) and how this line is delivered.
public sealed record CharacterDirection(string Voice, string Character, string Delivery, string Reason, VoiceDirector DirectedBy);

// Casts an NPC for gpt-4o-mini-tts and directs each line: an AI, or a plain default without one.
public interface ICharacterDirector
{
    Task<CharacterDirection> DirectAsync(CharacterDirectionRequest request);
}

public interface INpcCharacterRepository
{
    Task<NpcCharacter?> GetAsync(string npcName, string npcGender);
    Task SaveAsync(NpcCharacter character);
    Task DeleteAsync(string npcName, string npcGender);
}

// A speech engine the viewer can generate with, and whether it's set up.
public sealed record VoiceEngineInfo(VoiceEngine Id, string Name, bool Available);
