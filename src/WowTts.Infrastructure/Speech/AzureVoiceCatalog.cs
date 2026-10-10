using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;
using WowTts.Application.Abstractions;
using WowTts.Application.Exceptions;
using VoiceInfo = WowTts.Domain.Speech.VoiceInfo;

namespace WowTts.Infrastructure.Speech;

// The English voices of your Speech resource, with the styles each supports: the standard neural voices,
// or their Dragon HD Omni versions when Speech:VoiceModel is "DragonHDOmni".
// If an NPC's voice has no Omni version, casting picks a new one (see VoiceCastingService).
// Asked from Azure once and then kept in memory, because the list rarely changes.
public sealed class AzureVoiceCatalog(IOptions<SpeechOptions> options) : IVoiceCatalog
{
    private const string OmniSuffix = ":DragonHDOmniLatestNeural";

    // Every Dragon HD Omni voice supports these styles on English text (Microsoft Learn, "HD voices").
    private static readonly IReadOnlyList<string> OmniStyles =
    [
        "amazed", "amused", "angry", "annoyed", "anxious", "appreciative", "calm", "cautious", "concerned", "confident",
        "confused", "curious", "defeated", "defensive", "defiant", "determined", "disappointed", "disgusted", "doubtful",
        "ecstatic", "encouraging", "excited", "fast", "fearful", "frustrated", "happy", "hesitant", "hurt", "impatient",
        "impressed", "intrigued", "joking", "laughing", "optimistic", "painful", "panicked", "panting", "pleading", "proud",
        "quiet", "reassuring", "reflective", "relieved", "remorseful", "resigned", "sad", "sarcastic", "secretive", "serious",
        "shocked", "shouting", "shy", "skeptical", "slow", "struggling", "surprised", "suspicious", "sympathetic",
        "terrified", "upset", "urgent",
    ];

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

            var english = result.Voices.Where(voice => voice.Locale.StartsWith("en-", StringComparison.OrdinalIgnoreCase)).ToList();
            _voices = _options.UsesHdVoices ? OmniVoices(english) : StandardVoices(english);
            return _voices;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Standard neural voices. HD voices have a ':' in their name.
    private static List<VoiceInfo> StandardVoices(List<Microsoft.CognitiveServices.Speech.VoiceInfo> voices) => voices
        .Where(voice => !voice.ShortName.Contains(':'))
        .Select(voice => new VoiceInfo(
            voice.ShortName,
            voice.LocalName,
            voice.Gender.ToString(),
            voice.Locale,
            voice.StyleList.Where(style => style.Length > 0).ToList()))
        .ToList();

    // Azure lists only a few Omni voices, but most standard voices have one: en-GB-RyanNeural becomes
    // en-GB-Ryan:DragonHDOmniLatestNeural. Checked one by one on 9 Oct 2026: 86 of the 126 English voices have one.
    // Multilingual and Indic voices don't, and neither do these.
    private static readonly HashSet<string> WithoutOmni = new(StringComparer.OrdinalIgnoreCase)
    {
        "en-GB-Mia", "en-US-AIGenerate1", "en-US-AIGenerate2", "en-US-Blue",
    };

    private static bool HasOmniVersion(string voice) =>
        !voice.Contains("Multilingual", StringComparison.OrdinalIgnoreCase)
        && !voice.Contains("Indic", StringComparison.OrdinalIgnoreCase)
        && !WithoutOmni.Contains(VoiceInfo.PersonaOf(voice));

    private static List<VoiceInfo> OmniVoices(List<Microsoft.CognitiveServices.Speech.VoiceInfo> voices)
    {
        var fromStandard = voices
            .Where(voice => !voice.ShortName.Contains(':') && HasOmniVersion(voice.ShortName))
            .Select(voice => new VoiceInfo(
                VoiceInfo.PersonaOf(voice.ShortName) + OmniSuffix, voice.LocalName, voice.Gender.ToString(), voice.Locale, OmniStyles));

        var listed = voices
            .Where(voice => voice.ShortName.EndsWith(OmniSuffix, StringComparison.OrdinalIgnoreCase))
            .Select(voice => new VoiceInfo(voice.ShortName, voice.LocalName, voice.Gender.ToString(), voice.Locale, OmniStyles));

        return fromStandard.Concat(listed)
            .DistinctBy(voice => VoiceInfo.PersonaOf(voice.Name), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
