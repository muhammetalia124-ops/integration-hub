using IntegrationHub.Application.Contracts;
using IntegrationHub.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IntegrationHub.Data.Repositories;

public sealed class EfCurrencyRateRepository : ICurrencyRateRepository
{
    private readonly CurrencyRateDbContext _db;

    public EfCurrencyRateRepository(CurrencyRateDbContext db)
    {
        _db = db;
    }

    public Task<List<CurrencyRate>> GetAllAsync(CancellationToken ct) =>
        _db.CurrencyRates.AsNoTracking().ToListAsync(ct);

    public async Task ReplaceAllAsync(IReadOnlyCollection<CurrencyRate> rates, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await _db.CurrencyRates.ExecuteDeleteAsync(ct);
            await _db.CurrencyRates.AddRangeAsync(rates, ct);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
