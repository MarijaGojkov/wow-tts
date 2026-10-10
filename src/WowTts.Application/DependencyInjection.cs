using Microsoft.Extensions.DependencyInjection;
using WowTts.Application.Import;
using WowTts.Application.Maps;
using WowTts.Application.Quests;
using WowTts.Application.Speech;
using WowTts.Application.Voices;

namespace WowTts.Application;

public static class DependencyInjection
{
    // Registers the Application services (the use cases). Each phase adds its services here.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SpeechService>();
        services.AddScoped<QuestService>();
        services.AddScoped<QuestImportService>();
        services.AddScoped<AudioImportService>();
        services.AddScoped<TimingImportService>();
        services.AddScoped<GameDataImportService>();
        services.AddScoped<MapService>();
        services.AddScoped<VoiceCastingService>();

        return services;
    }
}
