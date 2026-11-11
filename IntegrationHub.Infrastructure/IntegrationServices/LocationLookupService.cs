using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.DTOs;

namespace IntegrationHub.Infrastructure.Integrations;

public sealed class LocationLookupService : ILocationLookupService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(30);
    private readonly HttpClient _http;
    private readonly IExternalApiCache _cache;

    public LocationLookupService(HttpClient http, IExternalApiCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<ResolvedLocationResponse?> ResolveAsync(
        double latitude,
        double longitude,
        CancellationToken ct)
    {
        var latitudeText = latitude.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var longitudeText = longitude.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var key = $"location:reverse:{Hash($"{latitudeText},{longitudeText}")}";
        return await _cache.GetOrCreateAsync(
            key,
            CacheLifetime,
            token => ResolveFromProviderAsync(latitudeText, longitudeText, token),
            _ => true,
            ct);
    }

    private async Task<ResolvedLocationResponse?> ResolveFromProviderAsync(
        string latitude,
        string longitude,
        CancellationToken ct)
    {
        var url = $"reverse?format=jsonv2&lat={latitude}&lon={longitude}&zoom=10&addressdetails=1";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!document.RootElement.TryGetProperty("address", out var address))
        {
            return null;
        }

        var city = GetAddressValue(address, "city")
                   ?? GetAddressValue(address, "province")
                   ?? GetAddressValue(address, "town")
                   ?? GetAddressValue(address, "village")
                   ?? GetAddressValue(address, "municipality");
        var district = GetAddressValue(address, "city_district")
                       ?? GetAddressValue(address, "district")
                       ?? GetAddressValue(address, "town")
                       ?? GetAddressValue(address, "municipality")
                       ?? GetAddressValue(address, "county");

        return string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(district)
            ? null
            : new ResolvedLocationResponse(city, district);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string? GetAddressValue(JsonElement address, string name)
    {
        return address.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
