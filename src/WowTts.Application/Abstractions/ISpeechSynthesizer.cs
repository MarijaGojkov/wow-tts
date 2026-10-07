namespace WowTts.Application.Abstractions;

// Turns text into audio. Application only knows this interface; Infrastructure provides the Azure version.
public interface ISpeechSynthesizer
{
    // Returns MP3 audio. When voice is null, the synthesizer uses its configured default voice.
    Task<byte[]> SynthesizeAsync(string text, string? voice);
}
