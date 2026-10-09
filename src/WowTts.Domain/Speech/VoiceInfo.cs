namespace WowTts.Domain.Speech;

// A voice the speech service offers, e.g. en-US-DavisNeural, and the speaking styles it supports
// (angry, cheerful, whispering…). Most voices support no styles at all.
public sealed record VoiceInfo(string Name, string DisplayName, string Gender, string Locale, IReadOnlyList<string> Styles);
