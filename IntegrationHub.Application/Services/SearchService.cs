using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.DTOs;

namespace IntegrationHub.Application.Services;

public sealed class SearchService
{
    private readonly IReadOnlyList<IIntegrationService> _services;
    private readonly UserActivityService _activities;

    public SearchService(IEnumerable<IIntegrationService> services, UserActivityService activities)
    {
        _services = services.ToList();
        _activities = activities;
    }

    public ServiceMetadataResponse GetMetadata()
    {
        var categories = _services
            .Where(service => service.IsEnabled)
            .OrderBy(service => service.DisplayName)
            .Select(service => service.BuildMetadata())
            .ToList();

        return new ServiceMetadataResponse { Categories = categories };
    }

    public async Task<CalculateResponse?> CalculateAsync(
        CalculateRequest request,
        int userId,
        CancellationToken ct)
    {
        var service = _services.FirstOrDefault(item =>
            item.ServiceType.Equals(request.ServiceType, StringComparison.OrdinalIgnoreCase) && item.IsEnabled);
        if (service is null)
        {
            return null;
        }

        var result = await service.CalculateAsync(request, userId, ct);
        if (result.Success)
        {
            result.SavedToHistory = await _activities.SaveIdempotentAsync(userId, result, ct);
        }

        return result;
    }
}
