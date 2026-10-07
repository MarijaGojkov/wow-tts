using Microsoft.Extensions.DependencyInjection;
using WowTts.Application.Speech;

namespace WowTts.Application;

public static class DependencyInjection
{
    // Registers the Application services (the use cases). Each phase adds its services here.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SpeechService>();

        return services;
    }
}
