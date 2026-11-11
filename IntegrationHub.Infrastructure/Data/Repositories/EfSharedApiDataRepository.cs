using System.Data;
using IntegrationHub.Application.Contracts;
using IntegrationHub.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Data.Repositories;

public sealed class EfSharedApiDataRepository : ISharedApiDataRepository
{
    private readonly CurrencyRateDbContext _db;

    public EfSharedApiDataRepository(CurrencyRateDbContext db)
    {
        _db = db;
    }

    public Task<List<SharedApiDataRecord>> GetListAsync(
        string? dataType,
        DateOnly? from,
        DateOnly? to,
        int take,
        CancellationToken ct)
    {
        var query = _db.SharedApiData.AsNoTracking().Where(record => !record.IsDeleted).AsQueryable();
        if (!string.IsNullOrWhiteSpace(dataType))
        {
            query = query.Where(record => record.DataType == dataType);
        }

        if (from.HasValue)
        {
            query = query.Where(record => record.DataDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(record => record.DataDate <= to.Value);
        }

        return query
            .OrderByDescending(record => record.DataDate)
            .ThenByDescending(record => record.FetchedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<SharedApiDataRecord?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.SharedApiData.FirstOrDefaultAsync(record => record.Id == id, ct);

    public Task<SharedApiDataRecord?> GetAdminManagedDailyAsync(
        string dataType,
        string recordKey,
        DateOnly dataDate,
        CancellationToken ct) =>
        _db.SharedApiData.AsNoTracking().FirstOrDefaultAsync(
            record => record.DataType == dataType &&
                      record.RecordKey == recordKey &&
                      record.DataDate == dataDate &&
                      !record.IsDeleted &&
                      record.IsAdminManaged,
            ct);

    public Task<bool> ExistsDailyAsync(
        string dataType,
        string recordKey,
        DateOnly dataDate,
        int exceptId,
        CancellationToken ct) =>
        _db.SharedApiData.AnyAsync(
            record => record.Id != exceptId &&
                      record.DataType == dataType &&
                      record.RecordKey == recordKey &&
                      record.DataDate == dataDate,
            ct);

    public async Task UpsertDailyAsync(SharedApiDataRecord record, CancellationToken ct)
    {
        await UpsertDailyRangeAsync(new[] { record }, ct);
    }

    public async Task UpsertDailyRangeAsync(
        IReadOnlyCollection<SharedApiDataRecord> records,
        CancellationToken ct)
    {
        if (records.Count == 0)
        {
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        foreach (var record in records)
        {
            var existing = await _db.SharedApiData.FirstOrDefaultAsync(
                item => item.DataType == record.DataType &&
                        item.RecordKey == record.RecordKey &&
                        item.DataDate == record.DataDate,
                ct);
            if (existing is null)
            {
                await _db.SharedApiData.AddAsync(record, ct);
            }
            else
            {
                if (existing.IsDeleted && !record.IsAdminManaged)
                {
                    continue;
                }

                if (existing.IsAdminManaged && !record.IsAdminManaged)
                {
                    continue;
                }

                existing.DisplayName = record.DisplayName;
                existing.PayloadJson = record.PayloadJson;
                existing.FetchedAtUtc = record.FetchedAtUtc;
                existing.IsAdminManaged = record.IsAdminManaged;
                existing.IsDeleted = false;
            }
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public Task AddAsync(SharedApiDataRecord record, CancellationToken ct) =>
        _db.SharedApiData.AddAsync(record, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

}
