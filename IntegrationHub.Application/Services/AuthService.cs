using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.Configuration;
using IntegrationHub.Application.DTOs;
using IntegrationHub.Models.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationHub.Application.Services;

public enum AuthOperationStatus
{
    Success,
    Conflict,
    Unauthorized
}

public sealed record AuthOperationResult(
    AuthOperationStatus Status,
    AuthResponse? Response = null,
    string? Error = null);

public sealed class AuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenIssuer _tokenIssuer;
    private readonly JwtSettings _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenIssuer tokenIssuer,
        IOptions<JwtSettings> jwt,
        ILogger<AuthService> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _jwt = jwt.Value;
        _logger = logger;
    }

    public async Task<AuthOperationResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (await _users.EmailExistsAsync(request.Email, null, ct))
        {
            return new AuthOperationResult(AuthOperationStatus.Conflict, Error: "Bu e-posta zaten kayıtlı.");
        }

        var user = new User
        {
            Email = request.Email,
            Username = request.Username,
            PasswordHash = _passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _users.AddAsync(user, ct);
        _logger.LogInformation("Yeni kullanıcı kaydı: {Email}", user.Email);
        return new AuthOperationResult(AuthOperationStatus.Success, BuildAuthResponse(user));
    }

    public async Task<AuthOperationResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct);
        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return new AuthOperationResult(
                AuthOperationStatus.Unauthorized,
                Error: "E-posta veya şifre hatalı.");
        }

        _logger.LogInformation("Kullanıcı giriş yaptı: {Email}", user.Email);
        return new AuthOperationResult(AuthOperationStatus.Success, BuildAuthResponse(user));
    }

    public async Task<AuthOperationResult> GetCurrentUserAsync(int userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
        {
            return new AuthOperationResult(
                AuthOperationStatus.Unauthorized,
                Error: "Kullanıcı hesabı etkin değil.");
        }

        return new AuthOperationResult(AuthOperationStatus.Success, BuildAuthResponse(user));
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        return new AuthResponse
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Token = _tokenIssuer.CreateToken(user, expiresAt),
            ExpiresAt = expiresAt,
            IsAdmin = user.IsAdmin
        };
    }
}
