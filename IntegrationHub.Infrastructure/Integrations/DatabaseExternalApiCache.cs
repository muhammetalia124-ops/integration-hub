using System.Text.Json;
using IntegrationHub.Application.Contracts;
using IntegrationHub.Models.Entities;

namespace IntegrationHub.Infrastructure.Integrations;

public sealed class DatabaseExternalApiCache : IExternalApiCache
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IExternalApiCacheRepository _repository;

    public DatabaseExternalApiCache(IExternalApiCacheRepository repository)
    {
        _repository = repository;
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string cacheKey,
        TimeSpan lifetime,
        Func<CancellationToken, Task<T?>> factory,
        Func<T, bool> shouldCache,
        CancellationToken ct)
        where T : class
    {
        var nowUtc = DateTime.UtcNow;
        var cached = await _repository.GetValidAsync(cacheKey, nowUtc, ct);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<T>(cached.PayloadJson, JsonOptions)
                   ?? throw new JsonException($"Cache verisi çözümlenemedi: {cacheKey}");
        }

        await RefreshLock.WaitAsync(ct);
        try
        {
            nowUtc = DateTime.UtcNow;
            cached = await _repository.GetValidAsync(cacheKey, nowUtc, ct);
            if (cached is not null)
            {
                return JsonSerializer.Deserialize<T>(cached.PayloadJson, JsonOptions)
                       ?? throw new JsonException($"Cache verisi çözümlenemedi: {cacheKey}");
            }

            var value = await factory(ct);
            if (value is not null && shouldCache(value))
            {
                var fetchedAtUtc = DateTime.UtcNow;
                await _repository.StoreAsync(new ExternalApiCacheEntry
                {
                    CacheKey = cacheKey,
                    PayloadJson = JsonSerializer.Serialize(value, JsonOptions),
                    FetchedAtUtc = fetchedAtUtc,
                    ExpiresAtUtc = fetchedAtUtc.Add(lifetime)
                }, ct);
            }

            return value;
        }
        finally
        {
            RefreshLock.Release();
        }
    }
}
