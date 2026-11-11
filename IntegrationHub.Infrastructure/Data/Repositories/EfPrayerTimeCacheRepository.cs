using IntegrationHub.Application.Contracts;
using IntegrationHub.Models.Entities;
using System.Data;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Data.Repositories;

public sealed class EfPrayerTimeCacheRepository : IPrayerTimeCacheRepository
{
    private readonly CurrencyRateDbContext _db;

    public EfPrayerTimeCacheRepository(CurrencyRateDbContext db)
    {
        _db = db;
    }

    public Task<PrayerTimeCache?> GetAsync(string districtId, DateOnly prayerDate, CancellationToken ct) =>
        _db.PrayerTimeCache
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.DistrictId == districtId && item.PrayerDate == prayerDate,
                ct);

    public async Task UpsertRangeAsync(IReadOnlyCollection<PrayerTimeCache> entries, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        foreach (var entry in entries)
        {
            var existing = await _db.PrayerTimeCache.FirstOrDefaultAsync(
                item => item.DistrictId == entry.DistrictId && item.PrayerDate == entry.PrayerDate,
                ct);
            if (existing is null)
            {
                await _db.PrayerTimeCache.AddAsync(entry, ct);
            }
            else
            {
                existing.City = entry.City;
                existing.District = entry.District;
                existing.Imsak = entry.Imsak;
                existing.Gunes = entry.Gunes;
                existing.Ogle = entry.Ogle;
                existing.Ikindi = entry.Ikindi;
                existing.Aksam = entry.Aksam;
                existing.Yatsi = entry.Yatsi;
                existing.FetchedAtUtc = entry.FetchedAtUtc;
            }
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
