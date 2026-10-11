using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Audio;
using WowTts.Application.Abstractions;
using WowTts.Application.Exceptions;
using WowTts.Infrastructure.Voices;

namespace WowTts.Infrastructure.Speech;

// gpt-4o-mini-tts on Azure OpenAI: speech that follows written instructions, so a line can be played by "an old
// dwarf with a thick Scottish brogue". Billed per audio length on Foundry (no free tier).
public sealed class OpenAiSpeechSynthesizer(IOptions<OpenAiTtsOptions> options) : IInstructedSpeechSynthesizer
{
    // Its voices and how they sound (OpenAI doesn't publish genders; these are how they come across).
    private static readonly IReadOnlyList<CharacterVoice> AllVoices =
    [
        new("onyx", "male, very deep and resonant, authoritative"),
        new("ash", "male, warm and confident"),
        new("echo", "male, clear and steady"),
        new("verse", "male, expressive and dynamic"),
        new("ballad", "male, soft and expressive, slightly British"),
        new("fable", "young, lively storyteller, British"),
        new("alloy", "neutral, balanced and versatile"),
        new("nova", "female, bright and energetic"),
        new("coral", "female, warm and friendly"),
        new("sage", "female, calm and thoughtful"),
        new("shimmer", "female, soft and gentle"),
    ];

    private readonly OpenAiTtsOptions _options = options.Value;

    public string Name => "GPT-4o mini TTS";

    public bool IsAvailable => _options.IsConfigured;

    public IReadOnlyList<CharacterVoice> Voices => AllVoices;

    public async Task<byte[]> SpeakAsync(string text, string voice, string instructions)
    {
        if (!IsAvailable)
            throw new InvalidRequestException($"{Name} isn't set up.");

        var client = new AudioClient(
            model: _options.DeploymentName,
            credential: new ApiKeyCredential(_options.ApiKey.Trim()),
            options: new OpenAIClientOptions { Endpoint = _options.V1Endpoint });

        // The SDK marks instructions as experimental (OPENAI001), but they're what gpt-4o-mini-tts is for.
#pragma warning disable OPENAI001
        var speechOptions = new SpeechGenerationOptions
        {
            Instructions = instructions,
            ResponseFormat = GeneratedSpeechFormat.Mp3,
        };
#pragma warning restore OPENAI001

        try
        {
            BinaryData audio = await client.GenerateSpeechAsync(text, new GeneratedSpeechVoice(voice), speechOptions);
            return audio.ToArray();
        }
        catch (ClientResultException ex)
        {
            throw new SpeechSynthesisException($"{Name} failed ({ex.Status}): {ex.Message}");
        }
    }
}
