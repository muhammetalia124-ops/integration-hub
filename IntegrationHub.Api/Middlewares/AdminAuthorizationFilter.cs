using IntegrationHub.Application.DTOs;
using IntegrationHub.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IntegrationHub.Middlewares;

public sealed class AdminAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly AdminAuthorizationService _authorizationService;

    public AdminAuthorizationFilter(AdminAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Items.TryGetValue("UserId", out var value) || value is not int userId)
        {
            context.Result = new UnauthorizedObjectResult(new ApiErrorResponse { Error = "Kimlik doğrulanamadı." });
            return;
        }

        var isAdmin = await _authorizationService.IsActiveAdminAsync(
            userId,
            context.HttpContext.RequestAborted);

        if (!isAdmin)
        {
            context.Result = new ObjectResult(new ApiErrorResponse { Error = "Bu işlem için yönetici yetkisi gerekiyor." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
