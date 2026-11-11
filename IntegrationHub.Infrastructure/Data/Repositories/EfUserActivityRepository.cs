using IntegrationHub.Application.Contracts;
using IntegrationHub.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Data.Repositories;

public sealed class EfUserActivityRepository : IUserActivityRepository
{
    private readonly UserActivityDbContext _db;

    public EfUserActivityRepository(UserActivityDbContext db)
    {
        _db = db;
    }

    public Task<List<UserActivity>> GetHistoryAsync(int userId, int take, CancellationToken ct) =>
        _db.UserActivities
            .Where(activity => activity.UserId == userId)
            .OrderByDescending(activity => activity.Timestamp)
            .Take(take)
            .ToListAsync(ct);

    public Task<UserActivity?> FindLatestMatchAsync(
        int userId,
        string serviceType,
        string subCategory,
        string? inputValue,
        string? resultValue,
        CancellationToken ct) =>
        _db.UserActivities
            .Where(activity => activity.UserId == userId
                               && activity.ServiceType == serviceType
                               && activity.SubCategory == subCategory
                               && activity.InputValue == inputValue
                               && activity.ResultValue == resultValue)
            .OrderByDescending(activity => activity.Timestamp)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(UserActivity activity, CancellationToken ct)
    {
        await _db.UserActivities.AddAsync(activity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task DeleteByUserAsync(int userId, CancellationToken ct) =>
        _db.UserActivities.Where(activity => activity.UserId == userId).ExecuteDeleteAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
