namespace WowTts.Domain.Speech;

// Who chose how a line is spoken: the AI voice director, the fallback when no AI is set up (or it failed),
// or nobody here, because the audio was imported from another addon (AI VoiceOver).
public enum VoiceDirector
{
    Default,
    Ai,
    Imported
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

    // Audio made elsewhere, e.g. by the AI VoiceOver addon. How it was spoken isn't known.
    public static VoicePerformance Imported(string source, string reason) =>
        new(source, null, 1.0, 0, 0, reason, VoiceDirector.Imported);

    // Keeps the performance believable: a style the voice doesn't have is dropped,
    // and strength, speed and pitch stay within limits that still sound natural.
    // HD voices decide strength, speed and pitch themselves, so those stay at normal.
    public VoicePerformance Within(VoiceInfo voice) => this with
    {
        Style = Style is not null && voice.Styles.Contains(Style, StringComparer.OrdinalIgnoreCase)
            ? voice.Styles.First(style => style.Equals(Style, StringComparison.OrdinalIgnoreCase))
            : null,
        StyleDegree = voice.IsHd ? 1.0 : Math.Round(Math.Clamp(StyleDegree, MinStyleDegree, MaxStyleDegree), 2),
        RatePercent = voice.IsHd ? 0 : Math.Clamp(RatePercent, -MaxRateChange, MaxRateChange),
        PitchPercent = voice.IsHd ? 0 : Math.Clamp(PitchPercent, -MaxPitchChange, MaxPitchChange),
    };
}
