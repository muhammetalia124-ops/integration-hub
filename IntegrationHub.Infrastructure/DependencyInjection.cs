using IntegrationHub.Application.Contracts;
using IntegrationHub.Data;
using IntegrationHub.Data.Repositories;
using IntegrationHub.Infrastructure.Integrations;
using IntegrationHub.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<UserDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("UserDbConnection")));
        services.AddDbContext<UserActivityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("ActivityDbConnection")));
        services.AddDbContext<CurrencyRateDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("CurrencyRatesConnection")));

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IUserActivityRepository, EfUserActivityRepository>();
        services.AddScoped<IPrayerTimeCacheRepository, EfPrayerTimeCacheRepository>();
        services.AddScoped<ICurrencyRateRepository, EfCurrencyRateRepository>();
        services.AddScoped<IExternalApiCacheRepository, EfExternalApiCacheRepository>();
        services.AddScoped<ISharedApiDataRepository, EfSharedApiDataRepository>();
        services.AddScoped<IExternalApiCache, DatabaseExternalApiCache>();
        services.AddScoped<IExchangeRateProvider, CurrencyRateProvider>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        services.AddIntegrationServices();
        services.AddHttpClient("TcmbRates", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("IntegrationHub/1.0");
        });
        services.AddHttpClient<ILocationLookupService, LocationLookupService>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("IntegrationHub/1.0");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tr");
        });

        return services;
    }
}
