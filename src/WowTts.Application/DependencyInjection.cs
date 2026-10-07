using Microsoft.Extensions.DependencyInjection;

namespace WowTts.Application;

public static class DependencyInjection
{
    // Registers the Application services (the use cases). Each phase adds its services here.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
