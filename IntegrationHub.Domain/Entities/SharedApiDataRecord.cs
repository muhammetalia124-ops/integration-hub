namespace IntegrationHub.Models.Entities;

public sealed class SharedApiDataRecord
{
    public int Id { get; set; }
    public string DataType { get; set; } = string.Empty;
    public string RecordKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateOnly DataDate { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime FetchedAtUtc { get; set; }
    public bool IsAdminManaged { get; set; }
    public bool IsDeleted { get; set; }
}
