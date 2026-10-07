using WowTts.Application.Abstractions;
using WowTts.Application.Exceptions;

namespace WowTts.Application.Speech;

public sealed class SpeechService(ISpeechSynthesizer synthesizer)
{
    // Azure bills per character, so a limit stops one request from costing much.
    // Long WoW quest texts are well under this.
    public const int MaxTextLength = 3000;

    public async Task<byte[]> SpeakAsync(SpeechRequest request)
    {
        string text = request.Text?.Trim() ?? "";

        if (text.Length == 0)
            throw new InvalidRequestException("Text can't be empty.");

        if (text.Length > MaxTextLength)
            throw new InvalidRequestException($"Text is {text.Length} characters. The maximum is {MaxTextLength}.");

        string? voice = string.IsNullOrWhiteSpace(request.Voice) ? null : request.Voice.Trim();

        return await synthesizer.SynthesizeAsync(text, voice);
    }
}
