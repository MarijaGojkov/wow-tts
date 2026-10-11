using WowTts.Domain.Speech;

namespace WowTts.Application.Abstractions;

// MP3 audio, and each word as the synthesizer spoke it, with when.
public sealed record SpokenAudio(byte[] Audio, IReadOnlyList<HeardWord> Words);

// Turns text into audio. Application only knows this interface; Infrastructure provides the Azure version.
public interface ISpeechSynthesizer
{
    // What the viewer calls it, e.g. "Dragon HD Omni".
    string Name { get; }

    // Returns MP3 audio. When voice is null, the synthesizer uses its configured default voice.
    Task<byte[]> SynthesizeAsync(string text, string? voice);

    // Returns MP3 audio of the text spoken the way the performance describes (voice, style, speed and pitch),
    // with the time each word is spoken.
    Task<SpokenAudio> PerformAsync(string text, VoicePerformance performance);
}
