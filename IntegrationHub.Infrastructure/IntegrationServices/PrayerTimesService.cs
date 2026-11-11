using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.DTOs;
using IntegrationHub.Application.Services;
using IntegrationHub.Models.Entities;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Integrations;

public sealed class PrayerTimesService : IIntegrationService
{
    private const string BaseUrl = "https://ezanvakti.emushaf.net";
    private const string TurkeyId = "2";
    private static readonly TimeSpan LocationCatalogCacheLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan ScheduleCacheLifetime = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IExternalApiCache _apiCache;
    private readonly IPrayerTimeCacheRepository _prayerTimeCache;
    private readonly ISharedApiDataRepository _sharedApiData;
    private readonly ILocationLookupService _locationLookup;
    private readonly ILogger<PrayerTimesService> _logger;

    public PrayerTimesService(
        HttpClient http,
        IExternalApiCache apiCache,
        IPrayerTimeCacheRepository prayerTimeCache,
        ISharedApiDataRepository sharedApiData,
        ILocationLookupService locationLookup,
        ILogger<PrayerTimesService> logger)
    {
        _http = http;
        _apiCache = apiCache;
        _prayerTimeCache = prayerTimeCache;
        _sharedApiData = sharedApiData;
        _locationLookup = locationLookup;
        _logger = logger;
    }

    public string ServiceType => "prayer";
    public string DisplayName => "Namaz Vakitleri";
    public string Icon => "prayer";
    public bool IsEnabled => true;

    public CategoryMetadata BuildMetadata() => new()
    {
        ServiceType = ServiceType,
        DisplayName = DisplayName,
        Icon = Icon,
        SubCategories =
        {
            new SubCategoryMetadata
            {
                Key = "daily",
                DisplayName = "Günlük Namaz Vakitleri",
                InputKind = "text",
                InputLabel = "İl, ilçe",
                InputPlaceholder = "Örn. Denizli",
                ResultLabel = "Bugünün vakitleri"
            }
        }
    };

    public async Task<CalculateResponse> CalculateAsync(
        CalculateRequest request,
        int userId,
        CancellationToken ct = default)
    {
        try
        {
            var location = await ResolveRequestedLocationAsync(request, ct);
            if (location is null)
            {
                return Failure("Namaz vakti için il ve ilçe girin (ör. Denizli, Pamukkale).");
            }

            var city = await GetCityAsync(location.City, ct);
            if (city is null)
            {
                return Failure("Girilen il Diyanet namaz vakti listesinde bulunamadı.");
            }

            var district = await GetDistrictAsync(city.SehirID, location.District, ct);
            if (district is null)
            {
                district = await GetDistrictAsync(city.SehirID, city.SehirAdi, ct);
            }

            if (district is null)
            {
                return Failure("Girilen ilçe bu il için Diyanet namaz vakti listesinde bulunamadı.");
            }

            var prayerDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
            var managedRecord = await _sharedApiData.GetAdminManagedDailyAsync(
                SharedApiDataTypes.Prayer,
                district.IlceID,
                prayerDate,
                ct);
            var cachedTimes = managedRecord is null
                ? await _prayerTimeCache.GetAsync(district.IlceID, prayerDate, ct)
                : JsonSerializer.Deserialize<PrayerTimeCache>(managedRecord.PayloadJson, JsonOptions)
                  ?? throw new JsonException("Yönetici tarafından düzenlenen namaz vakti kaydı okunamadı.");

            if (cachedTimes is null)
            {
                cachedTimes = await FetchAndCacheTimesAsync(city, district, prayerDate, ct);
            }

            if (cachedTimes is null)
            {
                return Failure("Bugünün namaz vakitleri Diyanet kaynağında bulunamadı.");
            }

            var inputLocation = $"{city.SehirAdi}, {district.IlceAdi}";
            var resultValue =
                $"İmsak {cachedTimes.Imsak} · Güneş {cachedTimes.Gunes} · Öğle {cachedTimes.Ogle} · " +
                $"İkindi {cachedTimes.Ikindi} · Akşam {cachedTimes.Aksam} · Yatsı {cachedTimes.Yatsi}";

            return new CalculateResponse
            {
                Success = true,
                ServiceType = ServiceType,
                SubCategory = "daily",
                InputLabel = "Konum",
                InputValue = inputLocation,
                ResultLabel = $"Bugünün namaz vakitleri ({prayerDate:dd.MM.yyyy})",
                ResultValue = resultValue,
                Message = "Diyanet verileri"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Diyanet namaz vakitleri alınamadı.");
            return Failure("Diyanet namaz vakti servisine ulaşılamadı. Lütfen tekrar deneyin.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Diyanet namaz vakti yanıtı işlenemedi.");
            return Failure("Diyanet namaz vakti verisi işlenemedi.");
        }
    }

    private async Task<RequestedLocation?> ResolveRequestedLocationAsync(
        CalculateRequest request,
        CancellationToken ct)
    {
        if (request.Latitude.HasValue != request.Longitude.HasValue)
        {
            return null;
        }

        var value = (request.City ?? request.InputValue ?? string.Empty).Trim();
        var parts = value.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts.All(part => !string.IsNullOrWhiteSpace(part)))
        {
            return new RequestedLocation(parts[0], parts[1]);
        }

        if (!value.Contains(',') && !string.IsNullOrWhiteSpace(value))
        {
            return new RequestedLocation(value, value);
        }

        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            var resolved = await _locationLookup.ResolveAsync(request.Latitude.Value, request.Longitude.Value, ct);
            return resolved is null
                ? null
                : new RequestedLocation(resolved.City, resolved.District);
        }

        return null;
    }

    private async Task<DiyanetCity?> GetCityAsync(string cityName, CancellationToken ct)
    {
        var cities = await GetAsync<List<DiyanetCity>>(
            $"{BaseUrl}/sehirler/{TurkeyId}",
            LocationCatalogCacheLifetime,
            ct);
        var normalizedName = Normalize(cityName);
        return cities.FirstOrDefault(city => Normalize(city.SehirAdi) == normalizedName);
    }

    private async Task<DiyanetDistrict?> GetDistrictAsync(
        string cityId,
        string districtName,
        CancellationToken ct)
    {
        var districts = await GetAsync<List<DiyanetDistrict>>(
            $"{BaseUrl}/ilceler/{cityId}",
            LocationCatalogCacheLifetime,
            ct);
        var normalizedName = Normalize(districtName);
        return districts.FirstOrDefault(district => Normalize(district.IlceAdi) == normalizedName);
    }

    private async Task<PrayerTimeCache?> FetchAndCacheTimesAsync(
        DiyanetCity city,
        DiyanetDistrict district,
        DateOnly prayerDate,
        CancellationToken ct)
    {
        var schedules = await GetAsync<List<DiyanetSchedule>>(
            $"{BaseUrl}/vakitler/{district.IlceID}",
            ScheduleCacheLifetime,
            ct);
        var schedule = schedules
            .Select(schedule => (Schedule: schedule, Date: ParsePrayerDate(schedule)))
            .FirstOrDefault(item => item.Date == prayerDate && HasPrayerTimes(item.Schedule));
        if (schedule.Schedule is null)
        {
            return null;
        }

        var entry = new PrayerTimeCache
        {
            DistrictId = district.IlceID,
            City = city.SehirAdi,
            District = district.IlceAdi,
            PrayerDate = prayerDate,
            Imsak = schedule.Schedule.Imsak,
            Gunes = schedule.Schedule.Gunes,
            Ogle = schedule.Schedule.Ogle,
            Ikindi = schedule.Schedule.Ikindi,
            Aksam = schedule.Schedule.Aksam,
            Yatsi = schedule.Schedule.Yatsi,
            FetchedAtUtc = DateTime.UtcNow
        };
        await _prayerTimeCache.UpsertRangeAsync(new[] { entry }, ct);

        await ArchivePrayerTimeAsync(entry, ct);
        return entry;
    }

    private Task ArchivePrayerTimeAsync(PrayerTimeCache entry, CancellationToken ct)
    {
        if (!HasPrayerTimes(entry))
        {
            return Task.CompletedTask;
        }

        return _sharedApiData.UpsertDailyAsync(new SharedApiDataRecord
        {
            DataType = SharedApiDataTypes.Prayer,
            RecordKey = entry.DistrictId,
            DisplayName = $"{entry.City}, {entry.District}",
            DataDate = entry.PrayerDate,
            PayloadJson = JsonSerializer.Serialize(entry, JsonOptions),
            FetchedAtUtc = entry.FetchedAtUtc
        }, ct);
    }

    private async Task<T> GetAsync<T>(string url, TimeSpan lifetime, CancellationToken ct)
        where T : class
    {
        var result = await _apiCache.GetOrCreateAsync(
            $"prayer:provider:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))}",
            lifetime,
            async token =>
            {
                using var response = await _http.GetAsync(url, token);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, token);
            },
            _ => true,
            ct);

        return result ?? throw new JsonException("Diyanet servisi boş yanıt döndürdü.");
    }

    private static DateOnly? ParsePrayerDate(DiyanetSchedule schedule)
    {
        var value = schedule.MiladiTarihUzunIso8601 ?? schedule.MiladiTarihKisaIso8601;
        if (!string.IsNullOrWhiteSpace(value))
        {
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            {
                return DateOnly.FromDateTime(dateTime.Date);
            }

            if (DateOnly.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var shortDate))
            {
                return shortDate;
            }
        }

        return DateOnly.TryParseExact(
            schedule.MiladiTarihKisa,
            "dd.MM.yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate)
            ? parsedDate
            : null;
    }

    private static bool HasPrayerTimes(DiyanetSchedule schedule) =>
        !string.IsNullOrWhiteSpace(schedule.Imsak)
        && !string.IsNullOrWhiteSpace(schedule.Gunes)
        && !string.IsNullOrWhiteSpace(schedule.Ogle)
        && !string.IsNullOrWhiteSpace(schedule.Ikindi)
        && !string.IsNullOrWhiteSpace(schedule.Aksam)
        && !string.IsNullOrWhiteSpace(schedule.Yatsi);

    private static bool HasPrayerTimes(PrayerTimeCache entry) =>
        !string.IsNullOrWhiteSpace(entry.Imsak)
        && !string.IsNullOrWhiteSpace(entry.Gunes)
        && !string.IsNullOrWhiteSpace(entry.Ogle)
        && !string.IsNullOrWhiteSpace(entry.Ikindi)
        && !string.IsNullOrWhiteSpace(entry.Aksam)
        && !string.IsNullOrWhiteSpace(entry.Yatsi);

    private sealed record RequestedLocation(string City, string District);

    private static string Normalize(string value)
    {
        var normalized = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static CalculateResponse Failure(string message) => new()
    {
        Success = false,
        ServiceType = "prayer",
        SubCategory = "daily",
        Message = message
    };

    private sealed record DiyanetCity(
        [property: JsonPropertyName("SehirAdi")] string SehirAdi,
        [property: JsonPropertyName("SehirID")] string SehirID);

    private sealed record DiyanetDistrict(
        [property: JsonPropertyName("IlceAdi")] string IlceAdi,
        [property: JsonPropertyName("IlceID")] string IlceID);

    private sealed record DiyanetSchedule
    {
        public string? MiladiTarihKisa { get; init; }
        public string? MiladiTarihKisaIso8601 { get; init; }
        public string? MiladiTarihUzunIso8601 { get; init; }
        public string Imsak { get; init; } = string.Empty;
        public string Gunes { get; init; } = string.Empty;
        public string Ogle { get; init; } = string.Empty;
        public string Ikindi { get; init; } = string.Empty;
        public string Aksam { get; init; } = string.Empty;
        public string Yatsi { get; init; } = string.Empty;
    }
}
