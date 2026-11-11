using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.DTOs;
using IntegrationHub.Models.Entities;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Application.Services;

public sealed class UserActivityService
{
    private readonly IUserActivityRepository _activities;
    private readonly ILogger<UserActivityService> _logger;

    public UserActivityService(IUserActivityRepository activities, ILogger<UserActivityService> logger)
    {
        _activities = activities;
        _logger = logger;
    }

    public async Task<List<UserActivityDto>> GetHistoryAsync(int userId, CancellationToken ct)
    {
        var activities = await _activities.GetHistoryAsync(userId, 200, ct);
        return activities.Select(activity => new UserActivityDto
        {
            Id = activity.Id,
            ServiceType = activity.ServiceType,
            SubCategory = activity.SubCategory,
            InputLabel = activity.InputLabel,
            InputValue = activity.InputValue,
            ResultLabel = activity.ResultLabel,
            ResultValue = activity.ResultValue,
            Timestamp = activity.Timestamp
        }).ToList();
    }

    public Task ClearHistoryAsync(int userId, CancellationToken ct) =>
        _activities.DeleteByUserAsync(userId, ct);

    public async Task<bool> SaveIdempotentAsync(int userId, CalculateResponse result, CancellationToken ct)
    {
        try
        {
            var last = await _activities.FindLatestMatchAsync(
                userId,
                result.ServiceType,
                result.SubCategory,
                result.InputValue,
                result.ResultValue,
                ct);

            if (last is not null)
            {
                last.Timestamp = DateTime.UtcNow;
                await _activities.SaveChangesAsync(ct);
                return false;
            }

            await _activities.AddAsync(new UserActivity
            {
                UserId = userId,
                ServiceType = result.ServiceType,
                SubCategory = result.SubCategory,
                InputLabel = result.InputLabel,
                InputValue = result.InputValue,
                ResultLabel = result.ResultLabel,
                ResultValue = result.ResultValue,
                Timestamp = DateTime.UtcNow
            }, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aktivite kaydı saklanamadı (userId={UserId})", userId);
            return false;
        }
    }
}
