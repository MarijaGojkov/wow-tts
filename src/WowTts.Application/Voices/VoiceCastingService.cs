using Microsoft.Extensions.Logging;
using WowTts.Application.Abstractions;
using WowTts.Application.Exceptions;
using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Application.Voices;

// Decides how a quest line is spoken, by whoever says it: the quest giver for accept, and the NPC you hand the quest
// in to for progress and complete. The first time an NPC speaks, the voice director casts their voice from
// all voices of their gender, and it's saved. After that the NPC keeps that voice, and only the style, speed and
// pitch are decided per line. Whatever the director answers is checked against the real voice list.
public sealed class VoiceCastingService(
    IVoiceCatalog catalog,
    IVoiceDirector director,
    INpcVoiceRepository npcVoices,
    ILogger<VoiceCastingService> logger)
{
    public async Task<VoicePerformance> DecideAsync(Quest quest, QuestPart part, string spokenText)
    {
        var voices = await catalog.GetVoicesAsync();
        if (voices.Count == 0)
            throw new SpeechSynthesisException("The speech service returned no voices.");

        QuestNpc speaker = quest.SpeakerOf(part);
        bool isNamedNpc = speaker.Name.Length > 0 && speaker.Name != "Unknown";
        NpcVoice? cast = isNamedNpc ? await npcVoices.GetAsync(speaker.Name, speaker.Gender) : null;
        // Matched by persona, so an NPC cast as en-US-TonyNeural keeps Tony when HD voices are switched on, and back.
        VoiceInfo? castVoice = cast is null
            ? null
            : voices.FirstOrDefault(voice => VoiceInfo.SamePersona(voice.Name, cast.Voice));

        IReadOnlyList<VoiceInfo> candidates = castVoice is not null ? [castVoice] : ForGender(voices, speaker.Gender);

        VoicePerformance performance = await director.DirectAsync(new VoiceDirectionRequest(
            quest.Title, speaker.Name, speaker.Gender, speaker.Race, part, spokenText, candidates, castVoice is not null));

        // The director must pick one of the offered voices. If it didn't, use the most expressive one instead.
        VoiceInfo? chosen = candidates.FirstOrDefault(voice => voice.Name.Equals(performance.Voice, StringComparison.OrdinalIgnoreCase));
        if (chosen is null)
        {
            logger.LogWarning("Quest {QuestId} {Part}: the voice director chose {Voice}, which isn't on offer; using {Fallback}",
                quest.Id, part, performance.Voice, candidates[0].Name);
            chosen = candidates[0];
        }

        performance = (performance with { Voice = chosen.Name }).Within(chosen);

        // Only the AI's casting is kept. A default voice isn't a casting decision, so once an AI is set up
        // it still gets to cast this NPC.
        if (castVoice is null && isNamedNpc && performance.DirectedBy == VoiceDirector.Ai)
            await npcVoices.SaveAsync(new NpcVoice(speaker.Name, speaker.Gender, chosen.Name, performance.Reason));

        logger.LogInformation(
            "Quest {QuestId} {Part}: {Npc} speaks as {Voice}, style {Style} x{Degree}, rate {Rate}%, pitch {Pitch}% ({By})",
            quest.Id, part, speaker.Name, performance.Voice, performance.Style ?? "none", performance.StyleDegree,
            performance.RatePercent, performance.PitchPercent, performance.DirectedBy);

        return performance;
    }

    // Voices of the NPC's gender, the most expressive (most styles) first. Unknown gender gets every voice.
    private static IReadOnlyList<VoiceInfo> ForGender(IReadOnlyList<VoiceInfo> voices, string gender)
    {
        var matching = voices.Where(voice => voice.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase)).ToList();
        return (matching.Count > 0 ? matching : voices)
            .OrderByDescending(voice => voice.Styles.Count)
            .ThenBy(voice => voice.Name)
            .ToList();
    }
}
