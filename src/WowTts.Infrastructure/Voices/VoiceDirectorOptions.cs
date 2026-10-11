namespace WowTts.Infrastructure.Voices;

// The Azure OpenAI deployment that directs the voices, from environment variables:
// AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY and AZURE_OPENAI_DEPLOYMENT (the deployment name in Microsoft Foundry).
// When any of them is missing, quest lines use the default voices instead.
public sealed class VoiceDirectorOptions : AzureOpenAiOptions
{
    public const string SectionName = "VoiceDirector";

    // How long a reasoning model (gpt-5, o-series) may think: "low", "medium" or "high". Low keeps each line fast.
    // Leave it empty for models that don't reason, such as gpt-4.1-mini, which reject the setting.
    public string ReasoningEffort { get; set; } = "low";
}
