using IntegrationHub.Application.Contracts;
using IntegrationHub.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Data.Repositories;

public sealed class EfExternalApiCacheRepository : IExternalApiCacheRepository
{
    private readonly CurrencyRateDbContext _db;

    public EfExternalApiCacheRepository(CurrencyRateDbContext db)
    {
        _db = db;
    }

    public Task<ExternalApiCacheEntry?> GetValidAsync(
        string cacheKey,
        DateTime nowUtc,
        CancellationToken ct) =>
        _db.ExternalApiCache
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entry => entry.CacheKey == cacheKey && entry.ExpiresAtUtc > nowUtc,
                ct);

    public async Task StoreAsync(ExternalApiCacheEntry entry, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await _db.ExternalApiCache
                .Where(existing => existing.CacheKey == entry.CacheKey || existing.ExpiresAtUtc <= entry.FetchedAtUtc)
                .ExecuteDeleteAsync(ct);
            await _db.ExternalApiCache.AddAsync(entry, ct);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
