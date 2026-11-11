using System.Globalization;
using System.Xml.Linq;

namespace IntegrationHub.Infrastructure.Integrations;

public static class TcmbClient
{
    private const string Url = "https://www.tcmb.gov.tr/kurlar/today.xml";

    public static async Task<Dictionary<string, TcmbCurrency>> FetchAsync(HttpClient http, CancellationToken ct = default)
    {
        using var resp = await http.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);

        var map = new Dictionary<string, TcmbCurrency>(StringComparer.OrdinalIgnoreCase);

        foreach (var currency in doc.Descendants("Currency"))
        {
            var code = currency.Attribute("CurrencyCode")?.Value;
            if (string.IsNullOrWhiteSpace(code)) continue;

            var name = currency.Element("CurrencyName")?.Value?.Trim() ?? code;
            var forexBuyingStr = currency.Element("ForexBuying")?.Value;
            var forexSellingStr = currency.Element("ForexSelling")?.Value;
            var bankBuyingStr = currency.Element("BankBuying")?.Value;
            var bankSellingStr = currency.Element("BankSelling")?.Value;

            decimal forexBuying = ParseDecimal(forexBuyingStr);
            decimal forexSelling = ParseDecimal(forexSellingStr);
            decimal bankBuying = ParseDecimal(bankBuyingStr);
            decimal bankSelling = ParseDecimal(bankSellingStr);

            if (forexBuying == 0m && bankBuying != 0m) forexBuying = bankBuying;
            if (forexSelling == 0m && bankSelling != 0m) forexSelling = bankSelling;

            map[code] = new TcmbCurrency(code, name, forexBuying, forexSelling);
        }

        return map;
    }

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }
}

public sealed record TcmbCurrency(string Code, string Name, decimal ForexBuying, decimal ForexSelling);
