namespace WowTts.Infrastructure.Voices;

// Where an Azure OpenAI deployment lives: its endpoint, key and deployment name in Microsoft Foundry.
// When any of them is missing, the feature that needs it isn't used.
//
// Foundry shows several URLs for the same resource, and any of them works as the endpoint:
//   https://my-resource.services.ai.azure.com/openai/v1/responses                   (a deployment's endpoint)
//   https://my-resource.openai.azure.com/                                          (Azure OpenAI endpoint)
//   https://my-resource.cognitiveservices.azure.com/openai/deployments/voice-director/chat/completions?api-version=…
//                                                                                   (an older Target URI)
//   https://my-resource.services.ai.azure.com/api/projects/my-project               (project endpoint)
// Only the resource's address is used, and the older Target URI also gives the deployment name.
public abstract class AzureOpenAiOptions
{
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Deployment { get; set; } = "";

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

    // The deployment setting, or else the name inside a Target URI (…/openai/deployments/<name>/…).
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

// gpt-4o-mini-tts on Azure OpenAI, for the second generate button: AZURE_OPENAI_TTS_ENDPOINT,
// AZURE_OPENAI_TTS_API_KEY and AZURE_OPENAI_TTS_DEPLOYMENT. Its own resource, because the model is offered in
// other regions (e.g. East US 2) than the voice director's.
public sealed class OpenAiTtsOptions : AzureOpenAiOptions
{
    public const string SectionName = "OpenAiTts";
}
