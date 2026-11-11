using IntegrationHub.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<AdminService>();
        services.AddScoped<UserActivityService>();
        services.AddScoped<AdminAuthorizationService>();
        services.AddScoped<SearchService>();
        services.AddScoped<SharedApiDataService>();
        return services;
    }
}
