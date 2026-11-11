using IntegrationHub.Middlewares;
using IntegrationHub.Application.DTOs;
using IntegrationHub.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Controllers;

[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(AdminAuthorizationFilter))]
public sealed class AdminController : ControllerBase
{
    private readonly AdminService _adminService;
    private readonly SharedApiDataService _sharedApiDataService;

    public AdminController(AdminService adminService, SharedApiDataService sharedApiDataService)
    {
        _adminService = adminService;
        _sharedApiDataService = sharedApiDataService;
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserResponse>>> GetUsers(CancellationToken ct) =>
        Ok(await _adminService.GetUsersAsync(ct));

    [HttpGet("users/{id:int}")]
    public async Task<ActionResult<AdminUserResponse>> GetUserById(int id, CancellationToken ct)
    {
        var result = await _adminService.GetUserByIdAsync(id, ct);
        return result.Status == AdminOperationStatus.Success
            ? Ok(result.Value)
            : MapError<AdminUserResponse>(result);
    }

    [HttpPost("users")]
    public async Task<ActionResult<AdminUserResponse>> CreateUser(
        [FromBody] CreateAdminUserRequest request,
        CancellationToken ct)
    {
        var result = await _adminService.CreateUserAsync(request, ct);
        if (result.Status != AdminOperationStatus.Success)
        {
            return MapError<AdminUserResponse>(result);
        }

        return CreatedAtAction(nameof(GetUserById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("users/{id:int}")]
    public async Task<ActionResult<AdminUserResponse>> UpdateUser(
        int id,
        [FromBody] UpdateAdminUserRequest request,
        CancellationToken ct)
    {
        var result = await _adminService.UpdateUserAsync(id, request, IsCurrentUser(id), ct);
        return result.Status == AdminOperationStatus.Success
            ? Ok(result.Value)
            : MapError<AdminUserResponse>(result);
    }

    [HttpPost("users/{id:int}/password")]
    public async Task<IActionResult> SetPassword(
        int id,
        [FromBody] SetAdminUserPasswordRequest request,
        CancellationToken ct)
    {
        var result = await _adminService.SetPasswordAsync(id, request, ct);
        return result.Status == AdminOperationStatus.Success ? NoContent() : MapErrorAction(result);
    }

    [HttpGet("users/{id:int}/history")]
    public async Task<ActionResult<List<UserActivityDto>>> GetHistory(int id, CancellationToken ct)
    {
        var result = await _adminService.GetHistoryAsync(id, ct);
        return result.Status == AdminOperationStatus.Success
            ? Ok(result.Value)
            : MapError<List<UserActivityDto>>(result);
    }

    [HttpDelete("users/{id:int}/history")]
    public async Task<IActionResult> ClearHistory(int id, CancellationToken ct)
    {
        var result = await _adminService.ClearHistoryAsync(id, ct);
        return result.Status == AdminOperationStatus.Success ? NoContent() : MapErrorAction(result);
    }

    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken ct)
    {
        var result = await _adminService.DeleteUserAsync(id, IsCurrentUser(id), ct);
        return result.Status == AdminOperationStatus.Success ? NoContent() : MapErrorAction(result);
    }

    [HttpGet("shared-data")]
    public async Task<ActionResult<List<SharedApiDataDto>>> GetSharedApiData(
        [FromQuery] string? type,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var result = await _sharedApiDataService.GetListAsync(type, from, to, take, ct);
        return result.Status == SharedApiDataOperationStatus.Success
            ? Ok(result.Value)
            : BadRequest(new ApiErrorResponse { Error = result.Error! });
    }

    [HttpGet("shared-data/{id:int}")]
    public async Task<ActionResult<SharedApiDataDto>> GetSharedApiDataById(int id, CancellationToken ct)
    {
        var result = await _sharedApiDataService.GetByIdAsync(id, ct);
        return result.Status == SharedApiDataOperationStatus.Success
            ? Ok(result.Value)
            : NotFound(new ApiErrorResponse { Error = result.Error! });
    }

    [HttpPost("shared-data")]
    public async Task<ActionResult<SharedApiDataDto>> CreateSharedApiData(
        [FromBody] UpsertSharedApiDataRequest request,
        CancellationToken ct)
    {
        var result = await _sharedApiDataService.CreateAsync(request, ct);
        return result.Status == SharedApiDataOperationStatus.Success
            ? Ok(result.Value)
            : BadRequest(new ApiErrorResponse { Error = result.Error! });
    }

    [HttpPut("shared-data/{id:int}")]
    public async Task<ActionResult<SharedApiDataDto>> UpdateSharedApiData(
        int id,
        [FromBody] UpsertSharedApiDataRequest request,
        CancellationToken ct)
    {
        var result = await _sharedApiDataService.UpdateAsync(id, request, ct);
        return result.Status switch
        {
            SharedApiDataOperationStatus.Success => Ok(result.Value),
            SharedApiDataOperationStatus.NotFound => NotFound(new ApiErrorResponse { Error = result.Error! }),
            _ => BadRequest(new ApiErrorResponse { Error = result.Error! })
        };
    }

    [HttpDelete("shared-data/{id:int}")]
    public async Task<IActionResult> DeleteSharedApiData(int id, CancellationToken ct)
    {
        var result = await _sharedApiDataService.DeleteAsync(id, ct);
        return result.Status switch
        {
            SharedApiDataOperationStatus.Success => NoContent(),
            SharedApiDataOperationStatus.NotFound => NotFound(new ApiErrorResponse { Error = result.Error! }),
            _ => BadRequest(new ApiErrorResponse { Error = result.Error! })
        };
    }

    private bool IsCurrentUser(int id) =>
        HttpContext.Items.TryGetValue("UserId", out var value) && value is int userId && userId == id;

    private ActionResult<T> MapError<T>(AdminOperationResult<T> result) =>
        result.Status switch
        {
            AdminOperationStatus.NotFound => NotFound(new ApiErrorResponse { Error = result.Error! }),
            AdminOperationStatus.Conflict => Conflict(new ApiErrorResponse { Error = result.Error! }),
            _ => throw new InvalidOperationException("Başarılı işlem hata yanıtı olarak eşlenemez.")
        };

    private IActionResult MapErrorAction<T>(AdminOperationResult<T> result) =>
        result.Status switch
        {
            AdminOperationStatus.NotFound => NotFound(new ApiErrorResponse { Error = result.Error! }),
            AdminOperationStatus.Conflict => Conflict(new ApiErrorResponse { Error = result.Error! }),
            _ => throw new InvalidOperationException("Başarılı işlem hata yanıtı olarak eşlenemez.")
        };
}
