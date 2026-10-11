using Microsoft.Extensions.Logging;
using WowTts.Application.Abstractions;
using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Application.Voices;

// Decides how a quest line is spoken with gpt-4o-mini-tts, by whoever says it. The first time an NPC speaks, the
// director picks a voice and writes their character (accent, age, temperament), and it's saved, so the NPC always
// sounds the same. Each line then gets its own delivery: emotion, tone and pace.
public sealed class CharacterCastingService(
    IInstructedSpeechSynthesizer speech,
    ICharacterDirector director,
    INpcCharacterRepository characters,
    ILogger<CharacterCastingService> logger)
{
    public async Task<VoicePerformance> DecideAsync(Quest quest, QuestPart part, string spokenText)
    {
        QuestNpc speaker = quest.SpeakerOf(part);
        bool isNamedNpc = speaker.Name.Length > 0 && speaker.Name != "Unknown";
        NpcCharacter? cast = isNamedNpc ? await characters.GetAsync(speaker.Name, speaker.Gender) : null;

        CharacterDirection direction = await director.DirectAsync(new CharacterDirectionRequest(
            quest.Title, speaker, part, spokenText, speech.Voices, cast));

        // A cast NPC keeps their voice and character; only the delivery is new.
        string voice = cast?.Voice ?? direction.Voice;
        string character = cast?.Character ?? direction.Character;

        if (!speech.Voices.Any(v => v.Name.Equals(voice, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning("Quest {QuestId} {Part}: the director chose {Voice}, which isn't on offer; using {Fallback}",
                quest.Id, part, voice, speech.Voices[0].Name);
            voice = speech.Voices[0].Name;
        }
        voice = voice.ToLowerInvariant();

        if (cast is null && isNamedNpc && direction.DirectedBy == VoiceDirector.Ai)
            await characters.SaveAsync(new NpcCharacter(speaker.Name, speaker.Gender, voice, character, direction.Reason));

        string instructions = $"Character: {character}\nDelivery: {direction.Delivery}";

        logger.LogInformation("Quest {QuestId} {Part}: {Npc} speaks as {Voice} ({Character}; {Delivery}) ({By})",
            quest.Id, part, speaker.Name, voice, character, direction.Delivery, direction.DirectedBy);

        return new VoicePerformance(
            voice, null, 1.0, 0, 0, direction.Reason, direction.DirectedBy, VoiceEngine.OpenAi, instructions);
    }
}
