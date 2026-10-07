namespace WowTts.Application.Speech;

// The body of POST /api/speech. Voice is optional, e.g. "en-US-JennyNeural".
public sealed record SpeechRequest(string Text, string? Voice = null);
