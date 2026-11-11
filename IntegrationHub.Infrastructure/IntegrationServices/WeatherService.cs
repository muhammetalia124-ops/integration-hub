using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.DTOs;
using IntegrationHub.Application.Services;
using IntegrationHub.Models.Entities;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Integrations;

public class WeatherService : IIntegrationService
{
    public string ServiceType => "weather";
    public string DisplayName => "Hava Durumu";
    public string Icon => "weather";
    public bool IsEnabled => true;

    private readonly HttpClient _http;
    private readonly IExternalApiCache _cache;
    private readonly ISharedApiDataRepository _sharedApiData;
    private readonly ILogger<WeatherService> _logger;
    private static readonly JsonSerializerOptions ArchiveJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions ReadArchiveJsonOptions = new(JsonSerializerDefaults.Web);

    public WeatherService(
        HttpClient http,
        IExternalApiCache cache,
        ISharedApiDataRepository sharedApiData,
        ILogger<WeatherService> logger)
    {
        _http = http;
        _cache = cache;
        _sharedApiData = sharedApiData;
        _logger = logger;
    }

    public CategoryMetadata BuildMetadata()
    {
        return new CategoryMetadata
        {
            ServiceType = ServiceType,
            DisplayName = DisplayName,
            Icon = Icon,
            SubCategories = new List<SubCategoryMetadata>
            {
                new()
                {
                    Key = "current",
                    DisplayName = "Güncel Hava Durumu",
                    InputKind = "text",
                    InputLabel = "Şehir",
                    InputPlaceholder = "Örn. Denizli",
                    ResultLabel = "Durum"
                }
            }
        };
    }

    public async Task<CalculateResponse> CalculateAsync(CalculateRequest request, int userId, CancellationToken ct = default)
    {
        var city = (request.City ?? request.InputValue ?? string.Empty).Trim();
        if (request.Latitude.HasValue != request.Longitude.HasValue)
        {
            return Failure("Konum koordinatlarının ikisi de gönderilmelidir.");
        }

        var hasCoordinates = request.Latitude.HasValue && request.Longitude.HasValue;
        if (hasCoordinates &&
            (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180))
        {
            return Failure("Geçerli bir konum bilgisi gönderin.");
        }

        if (string.IsNullOrWhiteSpace(city) && !hasCoordinates)
        {
            return Failure("Lütfen bir şehir adı girin.");
        }

        try
        {
            var location = hasCoordinates
                ? new WeatherLocation(
                    string.IsNullOrWhiteSpace(city) ? "Konum" : city,
                    request.Latitude!.Value,
                    request.Longitude!.Value)
                : await GetCityLocationAsync(city, ct);
            if (location is null)
            {
                return Failure("Girilen şehir hava durumu servisinde bulunamadı.");
            }

            var cacheIdentity =
                $"{location.Name.ToUpperInvariant()}:{location.Latitude.ToString("R", CultureInfo.InvariantCulture)}:" +
                location.Longitude.ToString("R", CultureInfo.InvariantCulture);
            var archivedOverride = await _sharedApiData.GetAdminManagedDailyAsync(
                SharedApiDataTypes.Weather,
                location.Name.Trim().ToUpperInvariant(),
                DateOnly.FromDateTime(DateTime.UtcNow),
                ct);
            if (archivedOverride is not null)
            {
                var overrideResponse = JsonSerializer.Deserialize<CalculateResponse>(
                    archivedOverride.PayloadJson,
                    ReadArchiveJsonOptions);
                if (overrideResponse is null || !overrideResponse.Success)
                {
                    throw new JsonException("Yönetici tarafından düzenlenen hava durumu kaydı okunamadı.");
                }

                return overrideResponse;
            }

            var cacheKey = $"weather:current:{Hash(cacheIdentity)}";
            return await _cache.GetOrCreateAsync(
                       cacheKey,
                       TimeSpan.FromMinutes(10),
                       token => FetchWeatherAsync(location, token),
                       response => response.Success,
                       ct)
                   ?? Failure("Hava durumu verisi şu an bulunamadı.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Hava durumu servisine ulaşılamadı.");
            return Failure("Hava durumu servisine ulaşılamadı. Lütfen tekrar deneyin.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Hava durumu yanıtı işlenemedi.");
            return Failure("Hava durumu verisi işlenemedi.");
        }
    }

    private async Task<WeatherLocation?> GetCityLocationAsync(string city, CancellationToken ct)
    {
        var cacheKey = $"weather:location:{Hash(city.ToUpperInvariant())}";
        return await _cache.GetOrCreateAsync(
            cacheKey,
            TimeSpan.FromDays(30),
            token => FetchCityLocationAsync(city, token),
            _ => true,
            ct);
    }

    private async Task<WeatherLocation?> FetchCityLocationAsync(string city, CancellationToken ct)
    {
        var cityDistrict = city.Split(',', 2, StringSplitOptions.TrimEntries);
        if (cityDistrict.Length == 2 &&
            !string.IsNullOrWhiteSpace(cityDistrict[0]) &&
            !string.IsNullOrWhiteSpace(cityDistrict[1]))
        {
            var districtLocations = await SearchLocationsAsync(cityDistrict[1], ct);
            var matchingDistrict = districtLocations.FirstOrDefault(location =>
                IsTurkey(location) && Matches(location.AdministrativeArea, cityDistrict[0]));
            if (matchingDistrict is not null)
            {
                return matchingDistrict with { Name = $"{cityDistrict[0]}, {cityDistrict[1]}" };
            }

            var cityLocations = await SearchLocationsAsync(cityDistrict[0], ct);
            var matchingCity = cityLocations.FirstOrDefault(location =>
                IsTurkey(location) && Matches(location.Name, cityDistrict[0]));
            return matchingCity;
        }

        var locations = await SearchLocationsAsync(city, ct);
        return locations.FirstOrDefault(IsTurkey) ?? locations.FirstOrDefault();
    }

    private async Task<List<WeatherLocation>> SearchLocationsAsync(string query, CancellationToken ct)
    {
        var url = "https://geocoding-api.open-meteo.com/v1/search" +
                  $"?name={Uri.EscapeDataString(query)}&count=10&language=tr&format=json";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!document.RootElement.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            return new List<WeatherLocation>();
        }

        var locations = new List<WeatherLocation>(results.GetArrayLength());
        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty("latitude", out var latitude) ||
                !result.TryGetProperty("longitude", out var longitude) ||
                !latitude.TryGetDouble(out var latitudeValue) ||
                !longitude.TryGetDouble(out var longitudeValue))
            {
                continue;
            }

            locations.Add(new WeatherLocation(
                result.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString() ?? query
                    : query,
                latitudeValue,
                longitudeValue,
                result.TryGetProperty("admin1", out var admin1Element)
                    ? admin1Element.GetString()
                    : null,
                result.TryGetProperty("country_code", out var countryElement)
                    ? countryElement.GetString()
                    : null));
        }

        return locations;
    }

    private async Task<CalculateResponse?> FetchWeatherAsync(WeatherLocation location, CancellationToken ct)
    {
        var latitude = location.Latitude.ToString("R", CultureInfo.InvariantCulture);
        var longitude = location.Longitude.ToString("R", CultureInfo.InvariantCulture);
        var url = "https://api.open-meteo.com/v1/forecast" +
                  $"?latitude={latitude}&longitude={longitude}" +
                  "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m" +
                  "&timezone=auto";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!document.RootElement.TryGetProperty("current", out var current))
        {
            throw new JsonException("Hava durumu yanıtında güncel koşullar bulunamadı.");
        }

        var temperature = current.GetProperty("temperature_2m").GetDouble();
        var feelsLike = current.GetProperty("apparent_temperature").GetDouble();
        var humidity = current.GetProperty("relative_humidity_2m").GetInt32();
        var windSpeed = current.GetProperty("wind_speed_10m").GetDouble();
        var weatherCode = current.GetProperty("weather_code").GetInt32();
        var resultValue = $"{location.Name}: {DescribeWeather(weatherCode)}, {temperature:0.#}°C " +
                          $"(hissedilen {feelsLike:0.#}°C), nem %{humidity}, rüzgar {windSpeed:0.#} km/sa";

        var weatherResult = new CalculateResponse
        {
            Success = true,
            ServiceType = ServiceType,
            SubCategory = "current",
            InputLabel = "Şehir",
            InputValue = location.Name,
            ResultLabel = "Durum",
            ResultValue = resultValue,
            Message = "Başarılı"
        };
        var weatherData = new
        {
            observedAt = current.GetProperty("time").GetString(),
            timezone = document.RootElement.TryGetProperty("timezone", out var timezoneElement)
                ? timezoneElement.GetString()
                : null,
            units = document.RootElement.TryGetProperty("current_units", out var unitsElement)
                ? unitsElement.Clone()
                : (JsonElement?)null,
            current = new
            {
                temperature_2m = temperature,
                relative_humidity_2m = humidity,
                apparent_temperature = feelsLike,
                weather_code = weatherCode,
                wind_speed_10m = windSpeed
            }
        };
        var fetchedAtUtc = DateTime.UtcNow;
        await _sharedApiData.UpsertDailyAsync(new SharedApiDataRecord
        {
            DataType = SharedApiDataTypes.Weather,
            RecordKey = location.Name.Trim().ToUpperInvariant(),
            DisplayName = location.Name,
            DataDate = DateOnly.FromDateTime(fetchedAtUtc),
            PayloadJson = JsonSerializer.Serialize(new
            {
                weatherResult.Success,
                weatherResult.ServiceType,
                weatherResult.SubCategory,
                weatherResult.InputLabel,
                weatherResult.InputValue,
                weatherResult.ResultLabel,
                weatherResult.ResultValue,
                weatherResult.Message,
                weatherData
            }, ArchiveJsonOptions),
            FetchedAtUtc = fetchedAtUtc
        }, ct);
        return weatherResult;
    }

    private static string DescribeWeather(int code) => code switch
    {
        0 => "Açık",
        1 => "Çoğunlukla açık",
        2 => "Parçalı bulutlu",
        3 => "Kapalı",
        45 or 48 => "Sisli",
        51 or 53 or 55 => "Çisenti",
        56 or 57 => "Dondurucu çisenti",
        61 or 63 or 65 => "Yağmurlu",
        66 or 67 => "Dondurucu yağmur",
        71 or 73 or 75 or 77 => "Karlı",
        80 or 81 or 82 => "Sağanak yağışlı",
        85 or 86 => "Kar sağanağı",
        95 => "Gök gürültülü fırtına",
        96 or 99 => "Dolu ihtimalli gök gürültülü fırtına",
        _ => "Bilinmiyor"
    };

    private static bool IsTurkey(WeatherLocation location) =>
        string.Equals(location.CountryCode, "TR", StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string? value, string expected) =>
        string.Equals(value?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

    private sealed record WeatherLocation(
        string Name,
        double Latitude,
        double Longitude,
        string? AdministrativeArea = null,
        string? CountryCode = null);

    private static CalculateResponse Failure(string message) => new()
    {
        Success = false,
        ServiceType = "weather",
        SubCategory = string.Empty,
        Message = message
    };

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
