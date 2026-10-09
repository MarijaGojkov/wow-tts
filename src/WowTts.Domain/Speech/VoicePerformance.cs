namespace WowTts.Domain.Speech;

// Who chose how a line is spoken: the AI voice director, or the fallback when no AI is set up (or it failed).
public enum VoiceDirector
{
    Default,
    Ai
}

// How one line is performed: the voice, an optional speaking style and its strength, and speed and pitch changes.
public sealed record VoicePerformance(
    string Voice,
    string? Style,
    double StyleDegree,
    int RatePercent,
    int PitchPercent,
    string Reason,
    VoiceDirector DirectedBy)
{
    public const double MinStyleDegree = 0.5;
    public const double MaxStyleDegree = 2.0;
    public const int MaxRateChange = 30;
    public const int MaxPitchChange = 30;

    // A plain reading in the given voice, with no style and no changes.
    public static VoicePerformance Plain(string voice, string reason) =>
        new(voice, null, 1.0, 0, 0, reason, VoiceDirector.Default);

    // Keeps the performance believable: a style the voice doesn't have is dropped,
    // and strength, speed and pitch stay within limits that still sound natural.
    public VoicePerformance Within(VoiceInfo voice) => this with
    {
        Style = Style is not null && voice.Styles.Contains(Style, StringComparer.OrdinalIgnoreCase)
            ? voice.Styles.First(style => style.Equals(Style, StringComparison.OrdinalIgnoreCase))
            : null,
        StyleDegree = Math.Round(Math.Clamp(StyleDegree, MinStyleDegree, MaxStyleDegree), 2),
        RatePercent = Math.Clamp(RatePercent, -MaxRateChange, MaxRateChange),
        PitchPercent = Math.Clamp(PitchPercent, -MaxPitchChange, MaxPitchChange),
    };
}
