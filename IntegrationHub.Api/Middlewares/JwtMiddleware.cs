using System.IdentityModel.Tokens.Jwt;
using System.Text;
using IntegrationHub.Application.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationHub.Middlewares;

public class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JwtSettings _settings;
    private readonly ILogger<JwtMiddleware> _logger;

    public JwtMiddleware(RequestDelegate next, IOptions<JwtSettings> settings, ILogger<JwtMiddleware> logger)
    {
        _next = next;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await WriteUnauthorized(context, "Token bulunamadı.");
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
            var handler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = handler.ValidateToken(token, parameters, out var validated);
            var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)
                              ?? principal.FindFirst("nameidentifier")
                              ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (userIdClaim is null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                await WriteUnauthorized(context, "Geçersiz token içeriği.");
                return;
            }

            context.Items["UserId"] = userId;
            context.User = principal;
            await _next(context);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning("JWT doğrulama hatası: {Message}", ex.Message);
            await WriteUnauthorized(context, "Geçersiz veya süresi dolmuş token.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT işlenirken beklenmeyen hata.");
            await WriteUnauthorized(context, "Kimlik doğrulama hatası.");
        }
    }

    private static bool IsPublicPath(string path)
    {
        if (path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/auth/register", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return true; // statik wwwroot dosyaları
        }

        return false;
    }

    private static async Task WriteUnauthorized(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new { error = message });
    }
}
