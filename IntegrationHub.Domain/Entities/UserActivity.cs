using System.ComponentModel.DataAnnotations;

namespace IntegrationHub.Models.Entities;

public class UserActivity
{
    public int Id { get; set; }

    public int UserId { get; set; }

    [Required]
    [MaxLength(64)]
    public string ServiceType { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string SubCategory { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? InputLabel { get; set; }

    [MaxLength(256)]
    public string? InputValue { get; set; }

    [MaxLength(128)]
    public string? ResultLabel { get; set; }

    [MaxLength(512)]
    public string? ResultValue { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class PrayerTimeCache
{
    public int Id { get; set; }

    [Required]
    [MaxLength(32)]
    public string DistrictId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string District { get; set; } = string.Empty;

    public DateOnly PrayerDate { get; set; }

    [Required]
    [MaxLength(5)]
    public string Imsak { get; set; } = string.Empty;

    [Required]
    [MaxLength(5)]
    public string Gunes { get; set; } = string.Empty;

    [Required]
    [MaxLength(5)]
    public string Ogle { get; set; } = string.Empty;

    [Required]
    [MaxLength(5)]
    public string Ikindi { get; set; } = string.Empty;

    [Required]
    [MaxLength(5)]
    public string Aksam { get; set; } = string.Empty;

    [Required]
    [MaxLength(5)]
    public string Yatsi { get; set; } = string.Empty;

    public DateTime FetchedAtUtc { get; set; } = DateTime.UtcNow;
}
