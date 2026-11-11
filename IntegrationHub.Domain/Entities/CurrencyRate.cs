namespace IntegrationHub.Models.Entities;

public sealed class CurrencyRate
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal ForexBuying { get; set; }
    public decimal ForexSelling { get; set; }
    public DateTime FetchedAtUtc { get; set; }
}
