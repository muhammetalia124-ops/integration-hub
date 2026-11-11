using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.Services;
using IntegrationHub.Data;
using IntegrationHub.Models.Entities;
using System.Text.Json;

namespace IntegrationHub.Infrastructure.Integrations;

public sealed class CurrencyRateProvider : IExchangeRateProvider
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    private readonly ICurrencyRateRepository _repository;
    private readonly ISharedApiDataRepository _sharedApiData;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CurrencyRateProvider(
        ICurrencyRateRepository repository,
        ISharedApiDataRepository sharedApiData,
        IHttpClientFactory httpClientFactory)
    {
        _repository = repository;
        _sharedApiData = sharedApiData;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyDictionary<string, CurrencyRate>> GetRatesAsync(CancellationToken ct)
    {
        var cachedRates = await _repository.GetAllAsync(ct);
        var todayUtc = DateTime.UtcNow.Date;
        if (IsCurrent(cachedRates, todayUtc))
        {
            await ArchiveRatesAsync(cachedRates, ct);
            return await ApplyAdminOverridesAsync(cachedRates, todayUtc, ct);
        }

        await RefreshLock.WaitAsync(ct);
        try
        {
            cachedRates = await _repository.GetAllAsync(ct);
            todayUtc = DateTime.UtcNow.Date;
            if (IsCurrent(cachedRates, todayUtc))
            {
                await ArchiveRatesAsync(cachedRates, ct);
                return await ApplyAdminOverridesAsync(cachedRates, todayUtc, ct);
            }

            var fetchedRates = await TcmbClient.FetchAsync(_httpClientFactory.CreateClient("TcmbRates"), ct);
            var fetchedAtUtc = DateTime.UtcNow;
            var rows = fetchedRates.Values
                .Select(rate => new CurrencyRate
                {
                    Code = rate.Code,
                    Name = rate.Name,
                    ForexBuying = rate.ForexBuying,
                    ForexSelling = rate.ForexSelling,
                    FetchedAtUtc = fetchedAtUtc
                })
                .ToList();

            if (rows.Count == 0)
            {
                throw new InvalidOperationException("TCMB kur yanıtında kaydedilecek kur bulunamadı.");
            }

            await _repository.ReplaceAllAsync(rows, ct);
            await ArchiveRatesAsync(rows, ct);
            return await ApplyAdminOverridesAsync(rows, fetchedAtUtc.Date, ct);
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private Task ArchiveRatesAsync(IEnumerable<CurrencyRate> rates, CancellationToken ct)
    {
        var records = rates.Select(rate => new SharedApiDataRecord
        {
            DataType = SharedApiDataTypes.Currency,
            RecordKey = rate.Code,
            DisplayName = rate.Name,
            DataDate = DateOnly.FromDateTime(rate.FetchedAtUtc),
            PayloadJson = JsonSerializer.Serialize(rate, JsonOptions),
            FetchedAtUtc = rate.FetchedAtUtc
        }).ToList();
        return _sharedApiData.UpsertDailyRangeAsync(records, ct);
    }

    private static bool IsCurrent(IReadOnlyCollection<CurrencyRate> rates, DateTime todayUtc) =>
        rates.Count > 0 && rates.All(rate => rate.FetchedAtUtc.Date == todayUtc);

    private async Task<IReadOnlyDictionary<string, CurrencyRate>> ApplyAdminOverridesAsync(
        IEnumerable<CurrencyRate> rates,
        DateTime todayUtc,
        CancellationToken ct)
    {
        var merged = rates.ToDictionary(rate => rate.Code, StringComparer.OrdinalIgnoreCase);
        var archived = await _sharedApiData.GetListAsync(
            SharedApiDataTypes.Currency,
            DateOnly.FromDateTime(todayUtc),
            DateOnly.FromDateTime(todayUtc),
            500,
            ct);
        foreach (var record in archived.Where(item => item.IsAdminManaged))
        {
            var rate = JsonSerializer.Deserialize<CurrencyRate>(
                record.PayloadJson,
                JsonOptions);
            if (rate is null || !string.Equals(rate.Code, record.RecordKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new JsonException($"Yönetici tarafından düzenlenen '{record.RecordKey}' kuru okunamadı.");
            }

            merged[rate.Code] = rate;
        }

        return merged;
    }

}
