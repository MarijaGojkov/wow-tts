using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Infrastructure.Persistence;

// One row of the NpcVoices table: the voice cast for an NPC (name + gender, since names repeat, e.g. "Guard").
public sealed class NpcVoiceEntity
{
    public int Id { get; set; }
    public string NpcName { get; set; } = "";
    public string NpcGender { get; set; } = "";
    public string Voice { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public NpcVoice ToDomain() => new(NpcName, NpcGender, Voice, Reason);
}

// One row of the NpcCharacters table: how an NPC is played with gpt-4o-mini-tts (voice and character).
public sealed class NpcCharacterEntity
{
    public int Id { get; set; }
    public string NpcName { get; set; } = "";
    public string NpcGender { get; set; } = "";
    public string Voice { get; set; } = "";
    public string Character { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public NpcCharacter ToDomain() => new(NpcName, NpcGender, Voice, Character, Reason);
}

// One row of the AudioPerformances table: how a quest part's audio was spoken.
public sealed class AudioPerformanceEntity
{
    public int QuestId { get; set; }
    public QuestPart Part { get; set; }
    public string Voice { get; set; } = "";
    public string? Style { get; set; }
    public double StyleDegree { get; set; }
    public int RatePercent { get; set; }
    public int PitchPercent { get; set; }
    public string Reason { get; set; } = "";
    public VoiceDirector DirectedBy { get; set; }

    // Null in rows from before there were two engines; those were all Azure Speech.
    public VoiceEngine? Engine { get; set; }
    public string? Instructions { get; set; }
    public DateTime CreatedAt { get; set; }

    public VoicePerformance ToDomain() =>
        new(Voice, Style, StyleDegree, RatePercent, PitchPercent, Reason, DirectedBy, Engine ?? VoiceEngine.Azure, Instructions);

    public static AudioPerformanceEntity From(int questId, QuestPart part, VoicePerformance performance)
    {
        var row = new AudioPerformanceEntity { QuestId = questId, Part = part };
        row.CopyFrom(performance);
        return row;
    }

    public void CopyFrom(VoicePerformance performance)
    {
        Voice = performance.Voice;
        Style = performance.Style;
        StyleDegree = performance.StyleDegree;
        RatePercent = performance.RatePercent;
        PitchPercent = performance.PitchPercent;
        Reason = performance.Reason;
        DirectedBy = performance.DirectedBy;
        Engine = performance.Engine;
        Instructions = performance.Instructions;
        CreatedAt = DateTime.UtcNow;
    }
}
