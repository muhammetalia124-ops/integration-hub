using System.Globalization;
using System.Text.Json;
using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.DTOs;
using IntegrationHub.Application.Services;
using IntegrationHub.Models.Entities;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Integrations;

public class GoldService : IIntegrationService
{
    public string ServiceType => "gold";
    public string DisplayName => "Altın";
    public string Icon => "gold";
    public bool IsEnabled => true;

    private static readonly TimeSpan PriceCacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly GoldProduct[] Products =
    {
        new("gram", "Gram Altın", "GA", "gram", "TRY"),
        new("ounce", "Ons Altın", "XAUUSD", "ons", "USD"),
        new("quarter", "Çeyrek Altın", "C", "adet", "TRY"),
        new("half", "Yarım Altın", "Y", "adet", "TRY"),
        new("full", "Tam Altın", "T", "adet", "TRY"),
        new("cumhuriyet", "Cumhuriyet Altını", "CMR", "adet", "TRY"),
        new("gremse", "Gremse Altın", "GR", "adet", "TRY"),
        new("has", "Has Altın", "XHGLD", "gram", "TRY"),
        new("22ayar", "22 Ayar Bilezik", "22", "gram", "TRY"),
        new("resat", "Reşat Altın", "RA", "adet", "TRY"),
        new("ata", "Ata Altın", "ATA", "adet", "TRY"),
        new("14ayar", "14 Ayar Altın", "14", "gram", "TRY"),
        new("18ayar", "18 Ayar Altın", "18", "gram", "TRY"),
        new("besli", "Beşli Altın", "BSL", "adet", "TRY"),
        new("hamit", "Hamit Altın", "HA", "adet", "TRY"),
        new("ikibucuk", "İkibuçuk Altın", "IKB", "adet", "TRY")
    };

    private readonly HttpClient _http;
    private readonly IExternalApiCache _cache;
    private readonly IExchangeRateProvider _rateProvider;
    private readonly ISharedApiDataRepository _sharedApiData;
    private readonly ILogger<GoldService> _logger;
    private static readonly JsonSerializerOptions ArchiveJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions ReadArchiveJsonOptions = new(JsonSerializerDefaults.Web);

    public GoldService(
        HttpClient http,
        IExternalApiCache cache,
        IExchangeRateProvider rateProvider,
        ISharedApiDataRepository sharedApiData,
        ILogger<GoldService> logger)
    {
        _http = http;
        _cache = cache;
        _rateProvider = rateProvider;
        _sharedApiData = sharedApiData;
        _logger = logger;
    }

    public CategoryMetadata BuildMetadata()
    {
        var cat = new CategoryMetadata
        {
            ServiceType = ServiceType,
            DisplayName = DisplayName,
            Icon = Icon
        };

        foreach (var product in Products)
        {
            cat.SubCategories.Add(new SubCategoryMetadata
            {
                Key = product.Key,
                DisplayName = product.DisplayName,
                InputKind = "bidirectional",
                InputLabel = "Miktar veya nakit",
                InputPlaceholder = "Miktar veya nakit girin",
                ResultLabel = "Sonuç",
                Options = new List<FieldOption>
                {
                    new() { Value = "to-tl", Label = "Miktar → TL" },
                    new() { Value = "to-count", Label = $"Nakit → {product.QuantityUnit}" }
                }
            });
        }

        return cat;
    }

    public async Task<CalculateResponse> CalculateAsync(CalculateRequest request, int userId, CancellationToken ct = default)
    {
        var product = Products.FirstOrDefault(item =>
            item.Key.Equals(request.SubCategory, StringComparison.OrdinalIgnoreCase));
        if (product is null)
        {
            return Failure("Desteklenmeyen altın tipi.");
        }

        var direction = request.Direction ?? "to-tl";
        if (!decimal.TryParse(request.InputValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var input) || input <= 0)
        {
            return Failure("Geçerli bir miktar girin.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        GoldPrice providerPrice;
        try
        {
            var managedUnitPrice = await _sharedApiData.GetAdminManagedDailyAsync(
                SharedApiDataTypes.Gold, product.Key, today, ct);
            if (managedUnitPrice is not null)
            {
                var overrideValue = JsonSerializer.Deserialize<GoldPriceArchive>(
                    managedUnitPrice.PayloadJson,
                    ReadArchiveJsonOptions);
                if (overrideValue is null)
                {
                    throw new JsonException($"Yönetici altın kaydı okunamadı: {product.Key}.");
                }

                providerPrice = overrideValue.ToPrice(product);
            }
            else
            {
                var snapshot = await GetPriceSnapshotAsync(ct);
                if (!snapshot.Prices.TryGetValue(product.Key, out providerPrice!))
                {
                    throw new JsonException($"Altın sağlayıcısı '{product.ProviderSymbol}' fiyatını döndürmedi.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Altın fiyatı alınamadı.");
            return Failure("Altın servisine ulaşılamadı. Lütfen tekrar deneyin.");
        }

        var price = direction == "to-tl" ? providerPrice.BuyingPrice : providerPrice.SellingPrice;
        var currency = providerPrice.Currency;
        if (currency == "USD")
        {
            var rates = await _rateProvider.GetRatesAsync(ct);
            if (!rates.TryGetValue("USD", out var usdRate))
            {
                return Failure("Ons altın için dolar kuru bulunamadı.");
            }

            var exchangeRate = direction == "to-tl" ? usdRate.ForexBuying : usdRate.ForexSelling;
            price = Math.Round(price * exchangeRate, 2, MidpointRounding.AwayFromZero);
            currency = "TRY";
        }

        if (price <= 0m)
        {
            return Failure("Altın fiyatı şu an bulunamadı.");
        }

        string resultValue;
        string resultLabel;
        string inputLabel;
        string inputValue;

        if (direction == "to-tl")
        {
            var tl = Math.Round(input * price, 2, MidpointRounding.AwayFromZero);
            resultValue = tl.ToString("0.##", CultureInfo.InvariantCulture);
            resultLabel = "TL";
            inputLabel = product.QuantityUnit;
            inputValue = input.ToString("0.####", CultureInfo.InvariantCulture);
        }
        else if (direction == "to-count")
        {
            var count = decimal.Floor(input / price);
            resultValue = count.ToString("0", CultureInfo.InvariantCulture);
            resultLabel = product.QuantityUnit;
            inputLabel = "Nakit (TL)";
            inputValue = input.ToString("0.##", CultureInfo.InvariantCulture);
        }
        else
        {
            return Failure("Geçersiz hesaplama yönü.");
        }

        return new CalculateResponse
        {
            Success = true,
            ServiceType = ServiceType,
            SubCategory = product.Key,
            InputLabel = inputLabel,
            InputValue = inputValue,
            ResultLabel = resultLabel,
            ResultValue = resultValue,
            RawDetail = $"Alış {providerPrice.BuyingPrice.ToString("0.##", CultureInfo.InvariantCulture)} {providerPrice.Currency} · " +
                        $"Satış {providerPrice.SellingPrice.ToString("0.##", CultureInfo.InvariantCulture)} {providerPrice.Currency} · " +
                        $"Hesaplama {price.ToString("0.##", CultureInfo.InvariantCulture)} TL/{product.QuantityUnit} · GenelPara",
            Message = "Başarılı"
        };
    }

    private async Task<GoldPriceSnapshot> GetPriceSnapshotAsync(CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync(
                   "gold:genelpara:all",
                   PriceCacheLifetime,
                   FetchAndArchivePricesAsync,
                   snapshot => snapshot.Prices.Count == Products.Length,
                   ct)
               ?? throw new InvalidOperationException("Altın fiyatları şu an alınamıyor.");
    }

    private async Task<GoldPriceSnapshot?> FetchAndArchivePricesAsync(CancellationToken ct)
    {
        var snapshot = await FetchPricesAsync(ct);
        var records = Products.Select(product =>
        {
            var price = snapshot.Prices[product.Key];
            return new SharedApiDataRecord
            {
                DataType = SharedApiDataTypes.Gold,
                RecordKey = product.Key,
                DisplayName = product.DisplayName,
                DataDate = DateOnly.FromDateTime(snapshot.FetchedAtUtc),
                PayloadJson = JsonSerializer.Serialize(GoldPriceArchive.From(price), ArchiveJsonOptions),
                FetchedAtUtc = snapshot.FetchedAtUtc
            };
        }).ToList();
        await _sharedApiData.UpsertDailyRangeAsync(records, ct);
        return snapshot;
    }

    private async Task<GoldPriceSnapshot> FetchPricesAsync(CancellationToken ct)
    {
        var symbols = string.Join(",", Products.Select(product => product.ProviderSymbol));
        using var response = await _http.GetAsync(
            $"https://api.genelpara.com/json/?list=altin&sembol={Uri.EscapeDataString(symbols)}",
            ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = document.RootElement;
        if (!root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("GenelPara altın yanıtı beklenen biçimde değil.");
        }

        var prices = new Dictionary<string, GoldPrice>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in Products)
        {
            if (!data.TryGetProperty(product.ProviderSymbol, out var quote) ||
                !TryReadPrice(quote, "alis", out var buyingPrice) ||
                !TryReadPrice(quote, "satis", out var sellingPrice) ||
                buyingPrice <= 0m || sellingPrice <= 0m)
            {
                throw new JsonException($"GenelPara '{product.ProviderSymbol}' için geçerli alış/satış fiyatı döndürmedi.");
            }

            var currency = quote.TryGetProperty("kur", out var currencyElement)
                ? currencyElement.GetString()
                : null;
            if (currency is null || !string.Equals(currency, product.Currency, StringComparison.Ordinal))
            {
                throw new JsonException($"GenelPara '{product.ProviderSymbol}' için beklenmeyen para birimi döndürdü.");
            }

            prices.Add(product.Key, new GoldPrice(
                buyingPrice,
                sellingPrice,
                currency,
                product.ProviderSymbol));
        }

        return new GoldPriceSnapshot(prices, DateTime.UtcNow);
    }

    private static bool TryReadPrice(JsonElement quote, string propertyName, out decimal price)
    {
        price = 0m;
        if (!quote.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
    }

    private sealed record GoldProduct(
        string Key,
        string DisplayName,
        string ProviderSymbol,
        string QuantityUnit,
        string Currency);

    private sealed record GoldPrice(
        decimal BuyingPrice,
        decimal SellingPrice,
        string Currency,
        string ProviderSymbol);

    private sealed record GoldPriceSnapshot(
        Dictionary<string, GoldPrice> Prices,
        DateTime FetchedAtUtc);

    private sealed record GoldPriceArchive(
        decimal? BuyingPrice,
        decimal? SellingPrice,
        string? Currency,
        string? ProviderSymbol,
        decimal? UnitPrice)
    {
        public GoldPrice ToPrice(GoldProduct product)
        {
            var legacyPrice = UnitPrice ?? 0m;
            return new GoldPrice(
                BuyingPrice ?? legacyPrice,
                SellingPrice ?? legacyPrice,
                Currency ?? product.Currency,
                ProviderSymbol ?? product.ProviderSymbol);
        }

        public static GoldPriceArchive From(GoldPrice price) =>
            new(price.BuyingPrice, price.SellingPrice, price.Currency, price.ProviderSymbol, null);
    }

    private static CalculateResponse Failure(string message) => new()
    {
        Success = false,
        ServiceType = "gold",
        SubCategory = string.Empty,
        Message = message
    };
}
