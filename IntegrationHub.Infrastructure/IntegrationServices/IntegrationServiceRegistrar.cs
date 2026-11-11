using System.Reflection;
using IntegrationHub.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Infrastructure.Integrations;

public static class IntegrationServiceRegistrar
{
    public static IServiceCollection AddIntegrationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var serviceType = typeof(IIntegrationService);

        var implementations = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && serviceType.IsAssignableFrom(t));

        foreach (var impl in implementations)
        {
            // Each integration service gets a named HttpClient created by the factory.
            var clientName = impl.FullName ?? impl.Name;
            services.AddHttpClient(clientName, c =>
            {
                c.Timeout = TimeSpan.FromSeconds(20);
                c.DefaultRequestHeaders.UserAgent.ParseAdd("IntegrationHub/1.0");
            });

            services.AddTransient(impl, sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient(clientName);
                return ActivatorUtilities.CreateInstance(sp, impl, client);
            });

            services.AddTransient(serviceType, sp => (IIntegrationService)sp.GetRequiredService(impl));
        }

        return services;
    }

}
