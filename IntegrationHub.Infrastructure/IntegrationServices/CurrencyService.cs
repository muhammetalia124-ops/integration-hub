using System.Globalization;
using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.DTOs;
using IntegrationHub.Models.Entities;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Integrations;

public class CurrencyService : IIntegrationService
{
    public string ServiceType => "currency";
    public string DisplayName => "Döviz";
    public string Icon => "currency";
    public bool IsEnabled => true;

    private static readonly string[] Supported =
    {
        "USD", "EUR", "GBP", "CHF", "SEK", "DKK", "NOK", "JPY", "SAR", "KWD", "AUD",
        "CAD", "RUB", "CNY", "PKR", "QAR", "KRW", "AZN", "AED", "RON", "KZT", "XDR"
    };
    private readonly IExchangeRateProvider _rateProvider;
    private readonly ILogger<CurrencyService> _logger;

    public CurrencyService(
        HttpClient _,
        IExchangeRateProvider rateProvider,
        ILogger<CurrencyService> logger)
    {
        _rateProvider = rateProvider;
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

        foreach (var code in Supported)
        {
            cat.SubCategories.Add(new SubCategoryMetadata
            {
                Key = code,
                DisplayName = CurrencyNames.TryGetValue(code, out var name) ? name : code,
                InputKind = "number",
                InputLabel = "Miktar (TL)",
                InputPlaceholder = "Örn. 1000",
                ResultLabel = "Sonuç"
            });
        }

        return cat;
    }

    public async Task<CalculateResponse> CalculateAsync(CalculateRequest request, int userId, CancellationToken ct = default)
    {
        var code = request.SubCategory;
        if (!Supported.Contains(code))
        {
            return Failure("Desteklenmeyen döviz kodu.");
        }

        if (!decimal.TryParse(request.InputValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var tlAmount) || tlAmount <= 0)
        {
            return Failure("Geçerli bir TL miktarı girin.");
        }

        IReadOnlyDictionary<string, CurrencyRate> rates;
        try
        {
            rates = await _rateProvider.GetRatesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCMB döviz kurları alınamadı.");
            return Failure("Kur servisine ulaşılamadı. Lütfen tekrar deneyin.");
        }

        if (!rates.TryGetValue(code, out var currency))
        {
            return Failure($"{code} kuru şu an bulunamadı.");
        }

        var sellingRate = currency.ForexSelling;
        if (sellingRate <= 0m && code == "XDR")
        {
            sellingRate = currency.ForexBuying;
        }

        if (sellingRate <= 0m)
        {
            return Failure($"{code} kuru şu an bulunamadı.");
        }

        var amount = Math.Round(tlAmount / sellingRate, 4, MidpointRounding.AwayFromZero);

        return new CalculateResponse
        {
            Success = true,
            ServiceType = ServiceType,
            SubCategory = code,
            InputLabel = "Miktar (TL)",
            InputValue = tlAmount.ToString("0.##", CultureInfo.InvariantCulture),
            ResultLabel = $"{currency.Name} ({code})",
            ResultValue = amount.ToString("0.####", CultureInfo.InvariantCulture),
            RawDetail = $"1 {code} = {sellingRate.ToString("0.####", CultureInfo.InvariantCulture)} TL (satış)",
            Message = "Başarılı"
        };
    }

    private static CalculateResponse Failure(string message) => new()
    {
        Success = false,
        ServiceType = "currency",
        SubCategory = string.Empty,
        Message = message
    };

    private static readonly IReadOnlyDictionary<string, string> CurrencyNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = "Amerikan Doları",
            ["EUR"] = "Euro",
            ["GBP"] = "İngiliz Sterlini",
            ["CHF"] = "İsviçre Frangı",
            ["SEK"] = "İsveç Kronu",
            ["DKK"] = "Danimarka Kronu",
            ["NOK"] = "Norveç Kronu",
            ["JPY"] = "Japon Yeni",
            ["SAR"] = "Suudi Arabistan Riyali",
            ["KWD"] = "Kuveyt Dinarı",
            ["AUD"] = "Avustralya Doları",
            ["CAD"] = "Kanada Doları",
            ["RUB"] = "Rus Rublesi",
            ["CNY"] = "Çin Yuanı",
            ["PKR"] = "Pakistan Rupisi",
            ["QAR"] = "Katar Riyali",
            ["KRW"] = "Güney Kore Wonu",
            ["AZN"] = "Azerbaycan Manatı",
            ["AED"] = "BAE Dirhemi",
            ["RON"] = "Romen Leyi",
            ["KZT"] = "Kazakistan Tengesi",
            ["XDR"] = "Özel Çekme Hakkı"
        };
}
