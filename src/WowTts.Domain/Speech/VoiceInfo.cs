namespace WowTts.Domain.Speech;

// A voice the speech service offers, e.g. en-US-DavisNeural, and the speaking styles it supports
// (angry, cheerful, whispering…). Most standard voices support no styles at all.
// HD voices have a ':' in their name, e.g. en-US-Davis:DragonHDOmniLatestNeural.
public sealed record VoiceInfo(string Name, string DisplayName, string Gender, string Locale, IReadOnlyList<string> Styles)
{
    // HD voices hear the emotion in the words themselves. They take a style, but not its strength, speed or pitch.
    public bool IsHd => IsHdName(Name);

    public static bool IsHdName(string voice) => voice.Contains(':');

    // The person behind a voice, the same for its standard and HD versions:
    // en-US-DavisNeural and en-US-Davis:DragonHDOmniLatestNeural are both en-US-Davis.
    public static string PersonaOf(string voice)
    {
        int colon = voice.IndexOf(':');
        string persona = colon >= 0 ? voice[..colon] : voice;
        return persona.EndsWith("Neural", StringComparison.OrdinalIgnoreCase) ? persona[..^"Neural".Length] : persona;
    }

    public static bool SamePersona(string voice, string other) =>
        PersonaOf(voice).Equals(PersonaOf(other), StringComparison.OrdinalIgnoreCase);
}
