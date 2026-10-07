using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WowTts.Infrastructure;

public static class DependencyInjection
{
    // Registers the real implementations of the Application interfaces: Azure Speech, audio files, quest data.
    // The configuration is passed in so settings such as the audio folder can come from appsettings.json.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
