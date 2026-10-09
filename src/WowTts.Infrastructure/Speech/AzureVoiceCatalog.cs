using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;
using WowTts.Application.Abstractions;
using WowTts.Application.Exceptions;
using VoiceInfo = WowTts.Domain.Speech.VoiceInfo;

namespace WowTts.Infrastructure.Speech;

// The English neural voices of your Speech resource, with the styles each supports.
// Asked from Azure once and then kept in memory, because the list rarely changes.
public sealed class AzureVoiceCatalog(IOptions<SpeechOptions> options) : IVoiceCatalog
{
    private readonly SpeechOptions _options = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<VoiceInfo>? _voices;

    public async Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync()
    {
        if (_voices is not null)
            return _voices;

        await _lock.WaitAsync();
        try
        {
            if (_voices is not null)
                return _voices;

            var config = SpeechConfig.FromSubscription(_options.Key, _options.Region);
            using var synthesizer = new SpeechSynthesizer(config, null as AudioConfig);
            using var result = await synthesizer.GetVoicesAsync("");

            if (result.Reason != ResultReason.VoicesListRetrieved)
                throw new SpeechSynthesisException($"Couldn't get the voice list from Azure Speech: {result.ErrorDetails}");

            // English, standard neural voices only. HD voices have a ':' in their name and don't take styles.
            _voices = result.Voices
                .Where(voice => voice.Locale.StartsWith("en-", StringComparison.OrdinalIgnoreCase) && !voice.ShortName.Contains(':'))
                .Select(voice => new VoiceInfo(
                    voice.ShortName,
                    voice.LocalName,
                    voice.Gender.ToString(),
                    voice.Locale,
                    voice.StyleList.Where(style => style.Length > 0).ToList()))
                .ToList();

            return _voices;
        }
        finally
        {
            _lock.Release();
        }
    }
}
