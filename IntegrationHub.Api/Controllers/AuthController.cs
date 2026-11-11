using IntegrationHub.Application.DTOs;
using IntegrationHub.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiErrorResponse { Error = "Geçersiz istek.", Detail = "Lütfen tüm alanları kurallara uygun doldurun." });
        }

        var result = await _authService.RegisterAsync(req, ct);
        return result.Status == AuthOperationStatus.Conflict
            ? Conflict(new ApiErrorResponse { Error = result.Error! })
            : Ok(result.Response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiErrorResponse { Error = "Geçersiz istek." });
        }

        var result = await _authService.LoginAsync(req, ct);
        return result.Status == AuthOperationStatus.Unauthorized
            ? Unauthorized(new ApiErrorResponse { Error = result.Error! })
            : Ok(result.Response);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthResponse>> Me(CancellationToken ct)
    {
        if (!HttpContext.Items.TryGetValue("UserId", out var userIdValue) || userIdValue is not int userId)
        {
            return Unauthorized(new ApiErrorResponse { Error = "Kimlik doğrulanamadı." });
        }

        var result = await _authService.GetCurrentUserAsync(userId, ct);
        return result.Status == AuthOperationStatus.Unauthorized
            ? Unauthorized(new ApiErrorResponse { Error = result.Error! })
            : Ok(result.Response);
    }
}
