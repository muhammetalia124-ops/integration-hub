namespace IntegrationHub.Models.Entities;

public sealed class ExternalApiCacheEntry
{
    public string CacheKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime FetchedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
