using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WowTts.Application.Abstractions;
using WowTts.Infrastructure.Persistence;
using WowTts.Infrastructure.Speech;
using WowTts.Infrastructure.Storage;
using WowTts.Infrastructure.Voices;

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
        services.AddSingleton<IVoiceCatalog, AzureVoiceCatalog>();

        // The voice director: a GPT model on Azure OpenAI when AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY and
        // AZURE_OPENAI_DEPLOYMENT are set, otherwise the default voices.
        services.AddOptions<VoiceDirectorOptions>()
            .Bind(configuration.GetSection(VoiceDirectorOptions.SectionName))
            .Configure(options =>
        {
            options.Endpoint = configuration["AZURE_OPENAI_ENDPOINT"] ?? "";
            options.ApiKey = configuration["AZURE_OPENAI_API_KEY"] ?? "";
            options.Deployment = configuration["AZURE_OPENAI_DEPLOYMENT"] ?? "";
        });
        services.AddSingleton<DefaultVoiceDirector>();
        services.AddSingleton<IVoiceDirector>(provider =>
            provider.GetRequiredService<IOptions<VoiceDirectorOptions>>().Value.IsConfigured
                ? ActivatorUtilities.CreateInstance<AzureOpenAiVoiceDirector>(provider)
                : provider.GetRequiredService<DefaultVoiceDirector>());
        services.AddScoped<INpcVoiceRepository, SqliteNpcVoiceRepository>();
        services.AddScoped<IPerformanceRepository, SqlitePerformanceRepository>();

        // Quests live in a SQLite file. Its path in appsettings.json is relative to the app's project folder.
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddDbContext<WowTtsDbContext>((provider, options) =>
        {
            var environment = provider.GetRequiredService<IHostEnvironment>();
            string file = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.File;
            string path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, file));
            options.UseSqlite($"Data Source={path}");
        });
        services.AddScoped<IQuestRepository, SqliteQuestRepository>();
        services.AddScoped<IGameDataRepository, SqliteGameDataRepository>();

        services.Configure<AudioStorageOptions>(configuration.GetSection(AudioStorageOptions.SectionName));
        services.AddSingleton<IAudioStore, FileAudioStore>();

        return services;
    }
}
