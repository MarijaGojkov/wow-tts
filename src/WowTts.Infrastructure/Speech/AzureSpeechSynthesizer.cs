using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Options;
using WowTts.Application.Abstractions;
using WowTts.Application.Exceptions;
using WowTts.Domain.Speech;

namespace WowTts.Infrastructure.Speech;

// The Azure version of ISpeechSynthesizer. This is the Phase 1 console code, now returning the audio
// as MP3 bytes instead of playing it through the speakers.
public sealed class AzureSpeechSynthesizer(IOptions<SpeechOptions> options) : ISpeechSynthesizer
{
    private readonly SpeechOptions _options = options.Value;

    public Task<byte[]> SynthesizeAsync(string text, string? voice) =>
        SpeakAsync(voice ?? _options.DefaultVoice, synthesizer => synthesizer.SpeakTextAsync(text));

    // Spoken through SSML, so the style, speed and pitch apply.
    public Task<byte[]> PerformAsync(string text, VoicePerformance performance) =>
        SpeakAsync(performance.Voice, synthesizer => synthesizer.SpeakSsmlAsync(SsmlBuilder.Build(text, performance)));

    private async Task<byte[]> SpeakAsync(string voice, Func<SpeechSynthesizer, Task<SpeechSynthesisResult>> speak)
    {
        var config = SpeechConfig.FromSubscription(_options.Key, _options.Region);
        config.SpeechSynthesisVoiceName = voice;
        config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio24Khz48KBitRateMonoMp3);

        // No audio config: the audio stays in memory (result.AudioData) instead of going to the speakers.
        using var synthesizer = new SpeechSynthesizer(config, audioConfig: null);
        using var result = await speak(synthesizer);

        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            return result.AudioData;

        if (result.Reason == ResultReason.Canceled)
        {
            var details = SpeechSynthesisCancellationDetails.FromResult(result);

            // BadRequest means Azure rejected what we sent, e.g. a voice name that doesn't exist.
            if (details.ErrorCode == CancellationErrorCode.BadRequest)
                throw new InvalidRequestException($"Azure Speech rejected the request: {details.ErrorDetails}");

            throw new SpeechSynthesisException(
                $"Azure Speech canceled the request ({details.ErrorCode}): {details.ErrorDetails}");
        }

        throw new SpeechSynthesisException($"Unexpected result from Azure Speech: {result.Reason}.");
    }
}
