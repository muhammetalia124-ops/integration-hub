using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.DTOs;
using IntegrationHub.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Controllers;

[ApiController]
[Authorize]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;
    private readonly UserActivityService _activityService;
    private readonly SharedApiDataService _sharedApiDataService;
    private readonly ILocationLookupService _locationLookup;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        SearchService searchService,
        UserActivityService activityService,
        SharedApiDataService sharedApiDataService,
        ILocationLookupService locationLookup,
        ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _activityService = activityService;
        _sharedApiDataService = sharedApiDataService;
        _locationLookup = locationLookup;
        _logger = logger;
    }

    [HttpGet("init")]
    public ActionResult<ServiceMetadataResponse> Init() => Ok(_searchService.GetMetadata());

    [HttpPost("location")]
    public async Task<ActionResult<ResolvedLocationResponse>> ResolveLocation(
        [FromBody] ResolveLocationRequest request,
        CancellationToken ct)
    {
        try
        {
            var location = await _locationLookup.ResolveAsync(request.Latitude, request.Longitude, ct);
            if (location is null)
            {
                return UnprocessableEntity(new ApiErrorResponse
                {
                    Error = "Konumunuzdan şehir ve ilçe bilgisi belirlenemedi."
                });
            }

            return Ok(location);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Konum bilgisi çözümlenemedi.");
            return StatusCode(502, new ApiErrorResponse
            {
                Error = "Konum servisine ulaşılamadı. Şehir ve ilçeyi elle girebilirsiniz."
            });
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Konum servisi geçersiz yanıt döndürdü.");
            return StatusCode(502, new ApiErrorResponse
            {
                Error = "Konum servisi geçersiz yanıt döndürdü. Şehir ve ilçeyi elle girebilirsiniz."
            });
        }
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<CalculateResponse>> Calculate(
        [FromBody] CalculateRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiErrorResponse
            {
                Error = "Geçersiz istek.",
                Detail = "serviceType ve subCategory zorunludur."
            });
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new ApiErrorResponse { Error = "Kimlik doğrulanamadı." });
        }

        try
        {
            var result = await _searchService.CalculateAsync(request, userId, ct);
            if (result is null)
            {
                return NotFound(new ApiErrorResponse
                {
                    Error = "Servis bulunamadı.",
                    Detail = $"'{request.ServiceType}' adlı servis kayıtlı değil."
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Service} servisi çalışırken hata fırlattı.", request.ServiceType);
            return StatusCode(502, new ApiErrorResponse
            {
                Error = "Servis çağrısı başarısız oldu.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<UserActivityDto>>> History(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new ApiErrorResponse { Error = "Kimlik doğrulanamadı." });
        }

        return Ok(await _activityService.GetHistoryAsync(userId, ct));
    }

    [HttpDelete("history")]
    public async Task<IActionResult> ClearOwnHistory(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new ApiErrorResponse { Error = "Kimlik doğrulanamadı." });
        }

        await _activityService.ClearHistoryAsync(userId, ct);
        return NoContent();
    }

    [HttpGet("shared-data")]
    public async Task<ActionResult<List<SharedApiDataDto>>> SharedData(
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

    private bool TryGetUserId(out int userId)
    {
        if (HttpContext.Items.TryGetValue("UserId", out var idObj) && idObj is int id)
        {
            userId = id;
            return true;
        }

        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                    ?? User.FindFirst("sub");
        if (claim is not null && int.TryParse(claim.Value, out userId))
        {
            return true;
        }

        userId = 0;
        return false;
    }
}
