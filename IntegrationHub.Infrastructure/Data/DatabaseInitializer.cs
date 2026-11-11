using System.Text.Json;
using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.Services;
using IntegrationHub.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Data;

public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly UserDbContext _users;
    private readonly UserActivityDbContext _activities;
    private readonly CurrencyRateDbContext _currencyRates;
    private readonly ICurrencyRateRepository _currencyRateRepository;
    private readonly ISharedApiDataRepository _sharedApiData;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        UserDbContext users,
        UserActivityDbContext activities,
        CurrencyRateDbContext currencyRates,
        ICurrencyRateRepository currencyRateRepository,
        ISharedApiDataRepository sharedApiData,
        IConfiguration configuration,
        ILogger<DatabaseInitializer> logger)
    {
        _users = users;
        _activities = activities;
        _currencyRates = currencyRates;
        _currencyRateRepository = currencyRateRepository;
        _sharedApiData = sharedApiData;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _users.Database.MigrateAsync(ct);
        await _currencyRates.Database.MigrateAsync(ct);
        await SyncExistingSharedDataAsync(ct);
        await _activities.Database.MigrateAsync(ct);
        await AdminAccountSeeder.SeedAsync(_users, _configuration, _logger, ct);
    }

    private async Task SyncExistingSharedDataAsync(CancellationToken ct)
    {
        var rates = await _currencyRateRepository.GetAllAsync(ct);
        if (rates.Count > 0)
        {
            await _sharedApiData.UpsertDailyRangeAsync(
                rates.Select(rate => new SharedApiDataRecord
                {
                    DataType = SharedApiDataTypes.Currency,
                    RecordKey = rate.Code,
                    DisplayName = rate.Name,
                    DataDate = DateOnly.FromDateTime(rate.FetchedAtUtc),
                    PayloadJson = JsonSerializer.Serialize(
                        rate,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    FetchedAtUtc = rate.FetchedAtUtc
                }).ToList(),
                ct);
        }

    }
}
