using IntegrationHub.Application.Contracts;

namespace IntegrationHub.Application.Services;

public sealed class AdminAuthorizationService
{
    private readonly IUserRepository _users;

    public AdminAuthorizationService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<bool> IsActiveAdminAsync(int userId, CancellationToken ct) =>
        (await _users.GetByIdAsync(userId, ct)) is { IsActive: true, IsAdmin: true };
}
