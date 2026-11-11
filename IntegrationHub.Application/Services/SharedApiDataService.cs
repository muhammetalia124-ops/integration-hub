using System.Globalization;
using System.Text.Json;
using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.DTOs;
using IntegrationHub.Models.Entities;

namespace IntegrationHub.Application.Services;

public static class SharedApiDataTypes
{
    public const string Currency = "currency";
    public const string Gold = "gold";
    public const string Weather = "weather";
    public const string Prayer = "prayer";

    public static readonly string[] All = { Currency, Gold, Weather, Prayer };
    public static readonly string[] GoldRecordKeys =
    {
        "gram", "ounce", "quarter", "half", "full", "cumhuriyet", "gremse", "has",
        "22ayar", "resat", "ata", "14ayar", "18ayar", "besli", "hamit", "ikibucuk"
    };
}

public enum SharedApiDataOperationStatus
{
    Success,
    NotFound,
    Invalid
}

public sealed record SharedApiDataOperationResult<T>(
    SharedApiDataOperationStatus Status,
    T? Value = default,
    string? Error = null);

public sealed class SharedApiDataService
{
    private readonly ISharedApiDataRepository _repository;

    public SharedApiDataService(ISharedApiDataRepository repository)
    {
        _repository = repository;
    }

    public async Task<SharedApiDataOperationResult<List<SharedApiDataDto>>> GetListAsync(
        string? dataType,
        DateOnly? from,
        DateOnly? to,
        int take,
        CancellationToken ct)
    {
        if (dataType is not null && !SharedApiDataTypes.All.Contains(dataType, StringComparer.OrdinalIgnoreCase))
        {
            return Invalid<List<SharedApiDataDto>>("Geçersiz veri kategorisi.");
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return Invalid<List<SharedApiDataDto>>("Başlangıç tarihi bitiş tarihinden sonra olamaz.");
        }

        var records = await _repository.GetListAsync(
            dataType?.ToLowerInvariant(),
            from,
            to,
            Math.Clamp(take, 1, 500),
            ct);
        return Success(records.Select(ToDto).ToList());
    }

    public async Task<SharedApiDataOperationResult<SharedApiDataDto>> CreateAsync(
        UpsertSharedApiDataRequest request,
        CancellationToken ct)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Invalid<SharedApiDataDto>(validationError);
        }

        var record = ToRecord(request);
        await _repository.UpsertDailyAsync(record, ct);
        var saved = await _repository.GetListAsync(
            record.DataType, record.DataDate, record.DataDate, 500, ct);
        return Success(ToDto(saved.Single(item => item.RecordKey == record.RecordKey)));
    }

    public async Task<SharedApiDataOperationResult<SharedApiDataDto>> UpdateAsync(
        int id,
        UpsertSharedApiDataRequest request,
        CancellationToken ct)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Invalid<SharedApiDataDto>(validationError);
        }

        var record = await _repository.GetByIdAsync(id, ct);
        if (record is null)
        {
            return NotFound<SharedApiDataDto>("Ortak veri kaydı bulunamadı.");
        }

        var dataType = request.DataType.Trim().ToLowerInvariant();
        var recordKey = request.RecordKey.Trim();
        if (await _repository.ExistsDailyAsync(dataType, recordKey, request.DataDate, id, ct))
        {
            return Invalid<SharedApiDataDto>("Aynı tür, kayıt anahtarı ve tarih için başka bir kayıt zaten var.");
        }

        record.DataType = dataType;
        record.RecordKey = recordKey;
        record.DisplayName = request.DisplayName.Trim();
        record.DataDate = request.DataDate;
        record.PayloadJson = request.PayloadJson;
        record.FetchedAtUtc = DateTime.UtcNow;
        record.IsAdminManaged = true;
        record.IsDeleted = false;
        await _repository.SaveChangesAsync(ct);
        return Success(ToDto(record));
    }

    public async Task<SharedApiDataOperationResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var record = await _repository.GetByIdAsync(id, ct);
        if (record is null)
        {
            return NotFound<bool>("Ortak veri kaydı bulunamadı.");
        }

        record.IsDeleted = true;
        record.IsAdminManaged = false;
        await _repository.SaveChangesAsync(ct);
        return Success(true);
    }

    public async Task<SharedApiDataOperationResult<SharedApiDataDto>> GetByIdAsync(
        int id,
        CancellationToken ct)
    {
        var record = await _repository.GetByIdAsync(id, ct);
        return record is null
            ? NotFound<SharedApiDataDto>("Ortak veri kaydı bulunamadı.")
            : Success(ToDto(record));
    }

    private static string? Validate(UpsertSharedApiDataRequest request)
    {
        var dataType = request.DataType.Trim().ToLowerInvariant();
        if (request.DataDate == default)
        {
            return "Veri tarihi zorunludur.";
        }

        if (!SharedApiDataTypes.All.Contains(dataType, StringComparer.Ordinal))
        {
            return "Desteklenmeyen ortak veri kategorisi.";
        }

        if (string.IsNullOrWhiteSpace(request.RecordKey) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return "Kayıt anahtarı ve görünen ad zorunludur.";
        }

        try
        {
            using var document = JsonDocument.Parse(request.PayloadJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return "Veri içeriği JSON nesnesi olmalıdır.";
            }

            return dataType switch
            {
                SharedApiDataTypes.Currency => ValidateCurrency(root, request.RecordKey),
                SharedApiDataTypes.Gold => ValidateGold(root, request.RecordKey),
                SharedApiDataTypes.Weather => ValidateWeather(root, request.RecordKey),
                SharedApiDataTypes.Prayer => ValidatePrayer(root, request.RecordKey, request.DataDate),
                _ => "Desteklenmeyen ortak veri kategorisi."
            };
        }
        catch (JsonException)
        {
            return "Veri içeriği geçerli JSON olmalıdır.";
        }
    }

    private static string? ValidateCurrency(JsonElement root, string recordKey)
    {
        var code = GetString(root, "code");
        var name = GetString(root, "name");
        if (string.IsNullOrWhiteSpace(code) || code.Length > 3 ||
            !string.Equals(code, recordKey.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(name) ||
            !GetPositiveDecimal(root, "forexBuying") ||
            !GetPositiveDecimal(root, "forexSelling"))
        {
            return "Döviz verisi code, name, pozitif forexBuying ve forexSelling alanlarını içermelidir.";
        }

        return null;
    }

    private static string? ValidateGold(JsonElement root, string recordKey)
    {
        if (!SharedApiDataTypes.GoldRecordKeys.Contains(recordKey.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return "Desteklenmeyen altın kayıt anahtarı.";
        }

        if (GetPositiveDecimal(root, "unitPrice"))
        {
            return null;
        }

        var currency = GetString(root, "currency");
        var expectedCurrency = string.Equals(recordKey.Trim(), "ounce", StringComparison.OrdinalIgnoreCase)
            ? "USD"
            : "TRY";
        return GetPositiveDecimal(root, "buyingPrice") &&
               GetPositiveDecimal(root, "sellingPrice") &&
               string.Equals(currency, expectedCurrency, StringComparison.OrdinalIgnoreCase)
            ? null
            : $"Altın verisi pozitif buyingPrice/sellingPrice ve {expectedCurrency} currency alanlarını içermelidir.";
    }

    private static string? ValidateWeather(JsonElement root, string recordKey)
    {
        var locationName = GetString(root, "inputValue");
        if (!GetBoolean(root, "success") ||
            string.IsNullOrWhiteSpace(locationName) ||
            !string.Equals(recordKey.Trim(), locationName?.Trim().ToUpperInvariant(), StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(GetString(root, "resultValue")) ||
            !TryGetProperty(root, "weatherData", out var weatherData) ||
            !TryGetProperty(weatherData, "current", out var current) ||
            !TryGetProperty(current, "temperature_2m", out _) ||
            !TryGetProperty(current, "relative_humidity_2m", out _) ||
            !TryGetProperty(current, "apparent_temperature", out _) ||
            !TryGetProperty(current, "weather_code", out _) ||
            !TryGetProperty(current, "wind_speed_10m", out _))
        {
            return "Hava durumu verisi konum, sonuç ve alınan tüm güncel ölçüm alanlarını içermelidir.";
        }

        if (HasProperty(root, "latitude") || HasProperty(root, "longitude"))
        {
            return "Hava durumu arşivine hassas koordinatlar kaydedilemez.";
        }

        return null;
    }

    private static string? ValidatePrayer(JsonElement root, string recordKey, DateOnly dataDate)
    {
        var districtId = GetString(root, "districtId");
        var dateText = GetString(root, "prayerDate");
        if (!string.Equals(districtId, recordKey.Trim(), StringComparison.Ordinal) ||
            !DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, out var prayerDate) ||
            prayerDate != dataDate ||
            string.IsNullOrWhiteSpace(GetString(root, "city")) ||
            string.IsNullOrWhiteSpace(GetString(root, "district")) ||
            !IsTime(root, "imsak") || !IsTime(root, "gunes") || !IsTime(root, "ogle") ||
            !IsTime(root, "ikindi") || !IsTime(root, "aksam") || !IsTime(root, "yatsi"))
        {
            return "Namaz verisi il, ilçe, districtId, tarih ve geçerli vakit alanlarını içermelidir.";
        }

        return null;
    }

    private static bool IsTime(JsonElement root, string name) =>
        TimeOnly.TryParseExact(
            GetString(root, name),
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

    private static bool GetPositiveDecimal(JsonElement root, string name) =>
        TryGetProperty(root, name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDecimal(out var number) &&
        number > 0;

    private static bool GetBoolean(JsonElement root, string name) =>
        TryGetProperty(root, name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? GetString(JsonElement root, string name) =>
        TryGetProperty(root, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool HasProperty(JsonElement root, string name) =>
        TryGetProperty(root, name, out _);

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static SharedApiDataRecord ToRecord(UpsertSharedApiDataRequest request) => new()
    {
        DataType = request.DataType.Trim().ToLowerInvariant(),
        RecordKey = request.RecordKey.Trim(),
        DisplayName = request.DisplayName.Trim(),
        DataDate = request.DataDate,
        PayloadJson = request.PayloadJson,
        FetchedAtUtc = DateTime.UtcNow,
        IsAdminManaged = true
    };

    private static SharedApiDataDto ToDto(SharedApiDataRecord record) => new()
    {
        Id = record.Id,
        DataType = record.DataType,
        RecordKey = record.RecordKey,
        DisplayName = record.DisplayName,
        DataDate = record.DataDate,
        PayloadJson = record.PayloadJson,
        FetchedAtUtc = record.FetchedAtUtc,
        IsAdminManaged = record.IsAdminManaged
    };

    private static SharedApiDataOperationResult<T> Success<T>(T value) =>
        new(SharedApiDataOperationStatus.Success, value);

    private static SharedApiDataOperationResult<T> NotFound<T>(string error) =>
        new(SharedApiDataOperationStatus.NotFound, Error: error);

    private static SharedApiDataOperationResult<T> Invalid<T>(string error) =>
        new(SharedApiDataOperationStatus.Invalid, Error: error);
}
