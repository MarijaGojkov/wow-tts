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

    public async Task<byte[]> SynthesizeAsync(string text, string? voice) =>
        (await SpeakAsync(voice ?? _options.DefaultVoice, synthesizer => synthesizer.SpeakTextAsync(text))).Audio;

    // Spoken through SSML, so the style, speed and pitch apply.
    public Task<SpokenAudio> PerformAsync(string text, VoicePerformance performance) =>
        SpeakAsync(performance.Voice, synthesizer => synthesizer.SpeakSsmlAsync(SsmlBuilder.Build(text, performance)));

    private async Task<SpokenAudio> SpeakAsync(string voice, Func<SpeechSynthesizer, Task<SpeechSynthesisResult>> speak)
    {
        var config = SpeechConfig.FromSubscription(_options.Key, _options.Region);
        config.SpeechSynthesisVoiceName = voice;
        config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio24Khz48KBitRateMonoMp3);

        // No audio config: the audio stays in memory (result.AudioData) instead of going to the speakers.
        using var synthesizer = new SpeechSynthesizer(config, audioConfig: null);

        // Azure reports each word as it speaks it. AudioOffset is in ticks (100 nanoseconds).
        var words = new List<HeardWord>();
        synthesizer.WordBoundary += (_, e) =>
        {
            if (e.BoundaryType != SpeechSynthesisBoundaryType.Word)
                return;
            int start = (int)(e.AudioOffset / TimeSpan.TicksPerMillisecond);
            lock (words)
                words.Add(new HeardWord(e.Text, start, start + (int)e.Duration.TotalMilliseconds));
        };

        using var result = await speak(synthesizer);

        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
        {
            lock (words)
                return new SpokenAudio(result.AudioData, words.OrderBy(word => word.StartMs).ToList());
        }

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
