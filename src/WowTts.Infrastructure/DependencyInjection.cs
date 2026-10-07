using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WowTts.Application.Abstractions;
using WowTts.Infrastructure.Speech;

namespace WowTts.Infrastructure;

public static class DependencyInjection
{
    // Registers the real implementations of the Application interfaces: Azure Speech, audio files, quest data.
    // The configuration is passed in so settings such as the audio folder can come from appsettings.json.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SpeechOptions>()
            .Bind(configuration.GetSection(SpeechOptions.SectionName))
            .Configure(options =>
            {
                // ASP.NET Core reads environment variables into configuration, so SPEECH_KEY is available here.
                options.Key = configuration["SPEECH_KEY"] ?? options.Key;
                options.Region = configuration["SPEECH_REGION"] ?? options.Region;
            })
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Key) && !string.IsNullOrWhiteSpace(options.Region),
                "SPEECH_KEY and SPEECH_REGION must both be set. Set them with setx, then open a new terminal.")
            .ValidateOnStart();

        services.AddSingleton<ISpeechSynthesizer, AzureSpeechSynthesizer>();

        return services;
    }
}
