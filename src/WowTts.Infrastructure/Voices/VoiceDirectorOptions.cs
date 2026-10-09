namespace WowTts.Infrastructure.Voices;

// The Azure OpenAI deployment that directs the voices, from environment variables:
// AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY and AZURE_OPENAI_DEPLOYMENT (the deployment name in Microsoft Foundry).
// When any of them is missing, quest lines use the default voices instead.
//
// Foundry shows several URLs for the same resource, and any of them works as the endpoint:
//   https://my-resource.services.ai.azure.com/openai/v1/responses                   (a deployment's endpoint)
//   https://my-resource.openai.azure.com/                                          (Azure OpenAI endpoint)
//   https://my-resource.cognitiveservices.azure.com/openai/deployments/voice-director/chat/completions?api-version=…
//                                                                                   (an older Target URI)
//   https://my-resource.services.ai.azure.com/api/projects/my-project               (project endpoint)
// Only the resource's address is used, and the older Target URI also gives the deployment name.
public sealed class VoiceDirectorOptions
{
    public const string SectionName = "VoiceDirector";

    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Deployment { get; set; } = "";

    // How long a reasoning model (gpt-5, o-series) may think: "low", "medium" or "high". Low keeps each line fast.
    // Leave it empty for models that don't reason, such as gpt-4.1-mini, which reject the setting.
    public string ReasoningEffort { get; set; } = "low";

    // Azure OpenAI's v1 API, which works with the standard OpenAI SDK and needs no api-version.
    public Uri? V1Endpoint => BaseEndpoint is Uri baseEndpoint ? new Uri(baseEndpoint, "openai/v1/") : null;

    public bool IsConfigured =>
        BaseEndpoint is not null && !string.IsNullOrWhiteSpace(ApiKey) && DeploymentName.Length > 0;

    // The resource's address. A Foundry project endpoint is turned into the resource's Azure OpenAI address.
    public Uri? BaseEndpoint
    {
        get
        {
            if (!Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out Uri? uri))
                return null;

            string host = uri.Host.EndsWith(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase)
                ? uri.Host[..^".services.ai.azure.com".Length] + ".openai.azure.com"
                : uri.Host;

            return new Uri($"https://{host}/");
        }
    }

    // AZURE_OPENAI_DEPLOYMENT, or else the name inside a Target URI (…/openai/deployments/<name>/…).
    public string DeploymentName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Deployment))
                return Deployment.Trim();

            if (!Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out Uri? uri))
                return "";

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            int index = Array.FindIndex(segments, segment => segment.Equals("deployments", StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < segments.Length ? Uri.UnescapeDataString(segments[index + 1]) : "";
        }
    }
}
