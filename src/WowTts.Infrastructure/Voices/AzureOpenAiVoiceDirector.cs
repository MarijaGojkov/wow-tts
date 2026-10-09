using System.ClientModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using WowTts.Application.Abstractions;
using WowTts.Domain.Speech;

namespace WowTts.Infrastructure.Voices;

// Asks a GPT model on Azure OpenAI to direct the voice: which voice suits the NPC, and how to speak this line
// (style, strength, speed, pitch). The answer must match a JSON schema, so it can be read reliably.
// It uses Azure OpenAI's v1 API through the standard OpenAI SDK, where the model name is the deployment name.
// If the call fails, the default director answers instead and the reason says why.
public sealed class AzureOpenAiVoiceDirector(
    IOptions<VoiceDirectorOptions> options,
    DefaultVoiceDirector fallback,
    ILogger<AzureOpenAiVoiceDirector> logger) : IVoiceDirector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly VoiceDirectorOptions _options = options.Value;

    private readonly ChatClient _chat = new(
        model: options.Value.DeploymentName,
        credential: new ApiKeyCredential(options.Value.ApiKey.Trim()),
        options: new OpenAIClientOptions { Endpoint = options.Value.V1Endpoint });

    private const string Instructions = """
        You are the voice director for World of Warcraft quest dialogue that is read aloud by Azure neural text-to-speech.
        For one line spoken by a quest giver, decide which voice speaks it and how.

        Casting (only when more than one voice is offered; otherwise use the one voice given):
        - Pick the voice that best fits the NPC: race, gender, role and personality, as their name and words suggest.
          The race may be "Unknown"; then judge from the name and the words.
        - Accents help: dwarves suit British or Irish voices (en-GB, en-IE); night elves calm, measured voices;
          gnomes and goblins quick, bright voices; orcs, trolls and tauren deep, rough voices; undead dry, cold voices;
          humans plain American or British voices.
        - Prefer voices that have styles, because they can carry emotion, unless an accent matters more.

        Performing the line:
        - style: one of the chosen voice's styles that matches the mood of this line, or null for a neutral reading.
          Don't force emotion: a calm request needs none. Use "shouting" only for real battle cries, "whispering" for
          secrets or quiet menace, "terrified" for real fear.
        - styleDegree: 1.0 is normal strength, up to 2.0 for very strong feeling, down to 0.5 for a hint of it.
        - ratePercent: -30 to 30. Slower for old, wise, grieving or menacing speakers; faster for excited or nervous
          ones. 0 when nothing stands out.
        - pitchPercent: -30 to 30. Lower for big races (orcs, tauren, trolls) and gruff speakers; higher for gnomes and
          for small, young or nervous speakers. 0 for most humans.
        - The part says when the line is said: "accept" (offering the quest), "progress" (the player came back before
          finishing), "complete" (the player handed it in, often thanks or relief).
        - reason: one short sentence, at most 20 words, saying why, for the person reviewing the audio.
        """;

    private const string Schema = """
        {
          "type": "object",
          "properties": {
            "voice": { "type": "string", "description": "The name of one of the offered voices, e.g. en-US-DavisNeural." },
            "style": { "type": ["string", "null"], "description": "One of that voice's styles, or null." },
            "styleDegree": { "type": "number" },
            "ratePercent": { "type": "integer" },
            "pitchPercent": { "type": "integer" },
            "reason": { "type": "string" }
          },
          "required": ["voice", "style", "styleDegree", "ratePercent", "pitchPercent", "reason"],
          "additionalProperties": false
        }
        """;

    public async Task<VoicePerformance> DirectAsync(VoiceDirectionRequest request)
    {
        try
        {
            var completionOptions = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "voice_direction", BinaryData.FromString(Schema), jsonSchemaIsStrict: true),
            };
            // The SDK marks reasoning effort as experimental (OPENAI001), but Azure's v1 API supports it for reasoning models.
#pragma warning disable OPENAI001
            if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
                completionOptions.ReasoningEffortLevel = new ChatReasoningEffortLevel(_options.ReasoningEffort.Trim().ToLowerInvariant());
#pragma warning restore OPENAI001

            ChatCompletion completion = await _chat.CompleteChatAsync(
                [new SystemChatMessage(Instructions), new UserChatMessage(Describe(request))], completionOptions);

            Answer answer = JsonSerializer.Deserialize<Answer>(completion.Content[0].Text, JsonOptions)
                ?? throw new JsonException("The voice director answered with nothing.");

            return new VoicePerformance(
                answer.Voice,
                string.IsNullOrWhiteSpace(answer.Style) ? null : answer.Style,
                answer.StyleDegree,
                answer.RatePercent,
                answer.PitchPercent,
                answer.Reason,
                VoiceDirector.Ai);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The AI voice director failed for {Npc} ({Part}); using the default voice", request.NpcName, request.Part);
            VoicePerformance plain = await fallback.DirectAsync(request);
            return plain with { Reason = $"The AI voice director failed ({ex.GetType().Name}: {ex.Message}), so the default voice was used." };
        }
    }

    // The request as JSON for the model: the quest, the NPC, the line, and the voices it may choose from.
    private static string Describe(VoiceDirectionRequest request) => JsonSerializer.Serialize(new
    {
        quest = request.QuestTitle,
        part = request.Part.ToString().ToLowerInvariant(),
        npc = new { name = request.NpcName, gender = request.NpcGender, race = request.NpcRace },
        line = request.Text,
        voiceAlreadyCast = request.VoiceIsCast,
        voices = request.Voices.Select(voice => new
        {
            name = voice.Name,
            gender = voice.Gender,
            locale = voice.Locale,
            styles = voice.Styles,
        }),
    }, JsonOptions);

    private sealed record Answer(string Voice, string? Style, double StyleDegree, int RatePercent, int PitchPercent, string Reason);
}
