using IntegrationHub.Application.DTOs;

namespace IntegrationHub.Application.Contracts;

public interface IIntegrationService
{
    string ServiceType { get; }
    string DisplayName { get; }
    string Icon { get; }
    bool IsEnabled { get; }

    CategoryMetadata BuildMetadata();
    Task<CalculateResponse> CalculateAsync(CalculateRequest request, int userId, CancellationToken ct = default);
}

public interface ILocationLookupService
{
    Task<ResolvedLocationResponse?> ResolveAsync(double latitude, double longitude, CancellationToken ct);
}
