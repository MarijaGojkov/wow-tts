using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using WowTts.Application.Abstractions;
using WowTts.Domain.Speech;

namespace WowTts.Infrastructure.Voices;

// Asks the voice director's GPT model to cast an NPC for gpt-4o-mini-tts and direct the line: which voice, who the
// character is (accent, age, temperament) and how this line is delivered. The answer must match a JSON schema.
// If the call fails, the default character director answers instead and the reason says why.
public sealed class OpenAiCharacterDirector(
    IOptions<VoiceDirectorOptions> options,
    DefaultCharacterDirector fallback,
    ILogger<OpenAiCharacterDirector> logger) : ICharacterDirector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly VoiceDirectorOptions _options = options.Value;

    private readonly ChatClient _chat = new(
        model: options.Value.DeploymentName,
        credential: new ApiKeyCredential(options.Value.ApiKey.Trim()),
        options: new OpenAIClientOptions { Endpoint = options.Value.V1Endpoint });

    private const string Instructions = """
        You direct World of Warcraft quest dialogue for gpt-4o-mini-tts, a speech model that follows written
        instructions about who is speaking and how. For one line spoken by an NPC, decide:

        voice: one of the offered voices that suits the NPC (gender, size, age). Deep voices (onyx) for orcs, tauren,
          trolls and big, old or menacing NPCs; bright ones (nova, fable) for gnomes, goblins and young NPCs.
          If "cast" is given, the NPC already has a voice and character: repeat them exactly.

        character: who is speaking, in at most 35 words, written as instructions to an actor: race, age, temperament
          and above all the accent and voice texture. Classic Warcraft accents: dwarves a thick Scottish brogue;
          gnomes quick, high and excitable; goblins fast, nasal, scheming New-York-hustler; night elves calm, ancient,
          slightly otherworldly; humans plain and grounded; orcs deep, rough and guttural; trolls a laid-back
          Caribbean lilt; tauren very deep, slow, warm and earthy; undead (Forsaken) dry, raspy and cold.
          For creature types (Dragonkin, Demon, Elemental, Giant, Mechanical) make it fit: regal and booming, menacing,
          echoing, slow and huge, flat and robotic. Use the name and words when the race is "Unknown".

        delivery: how this particular line is said, in at most 25 words: emotion, tone, pace, pauses, emphasis.
          The part says when it's said: "accept" (offering the quest), "progress" (the player came back before
          finishing), "complete" (the player handed it in, often thanks or relief). Keep it believable, not hammy.

        reason: one short sentence, at most 20 words, saying why, for the person reviewing the audio.
        """;

    private const string Schema = """
        {
          "type": "object",
          "properties": {
            "voice": { "type": "string", "description": "The name of one of the offered voices, e.g. onyx." },
            "character": { "type": "string" },
            "delivery": { "type": "string" },
            "reason": { "type": "string" }
          },
          "required": ["voice", "character", "delivery", "reason"],
          "additionalProperties": false
        }
        """;

    public async Task<CharacterDirection> DirectAsync(CharacterDirectionRequest request)
    {
        try
        {
            var completionOptions = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "character_direction", BinaryData.FromString(Schema), jsonSchemaIsStrict: true),
            };
#pragma warning disable OPENAI001
            if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
                completionOptions.ReasoningEffortLevel = new ChatReasoningEffortLevel(_options.ReasoningEffort.Trim().ToLowerInvariant());
#pragma warning restore OPENAI001

            ChatCompletion completion = await _chat.CompleteChatAsync(
                [new SystemChatMessage(Instructions), new UserChatMessage(Describe(request))], completionOptions);

            Answer answer = JsonSerializer.Deserialize<Answer>(completion.Content[0].Text, JsonOptions)
                ?? throw new JsonException("The character director answered with nothing.");

            return new CharacterDirection(answer.Voice, answer.Character, answer.Delivery, answer.Reason, VoiceDirector.Ai);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The AI character director failed for {Npc} ({Part}); using a default character",
                request.Npc.Name, request.Part);
            CharacterDirection plain = await fallback.DirectAsync(request);
            return plain with { Reason = $"The AI character director failed ({ex.GetType().Name}: {ex.Message}), so a default was used." };
        }
    }

    // The request as JSON for the model: the quest, the NPC, the line, the voices, and the NPC's saved casting.
    private static string Describe(CharacterDirectionRequest request) => JsonSerializer.Serialize(new
    {
        quest = request.QuestTitle,
        part = request.Part.ToString().ToLowerInvariant(),
        npc = new { name = request.Npc.Name, gender = request.Npc.Gender, race = request.Npc.Race },
        line = request.Text,
        cast = request.Cast is NpcCharacter cast ? new { voice = cast.Voice, character = cast.Character } : null,
        voices = request.Voices.Select(voice => new { name = voice.Name, sounds = voice.Description }),
    }, JsonOptions);

    private sealed record Answer(string Voice, string Character, string Delivery, string Reason);
}
