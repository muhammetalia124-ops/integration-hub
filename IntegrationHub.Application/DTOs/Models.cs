using System.ComponentModel.DataAnnotations;

namespace IntegrationHub.Application.DTOs;

public class RegisterRequest
{
    [Required]
    [EmailAddressWithDomain]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(3)]
    [MaxLength(128)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required]
    [EmailAddressWithDomain]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsAdmin { get; set; }
}

public class CreateAdminUserRequest
{
    [Required]
    [EmailAddressWithDomain]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(3)]
    [MaxLength(128)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class UpdateAdminUserRequest
{
    [Required]
    [EmailAddressWithDomain]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(3)]
    [MaxLength(128)]
    public string Username { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
}

public class SetAdminUserPasswordRequest
{
    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class AdminUserResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ApiErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

public class ServiceMetadataResponse
{
    public List<CategoryMetadata> Categories { get; set; } = new();
}

public class CategoryMetadata
{
    public string ServiceType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon { get; set; } = "calculator";
    public List<SubCategoryMetadata> SubCategories { get; set; } = new();
}

public class SubCategoryMetadata
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string InputKind { get; set; } = "number";
    public string InputLabel { get; set; } = "Miktar";
    public string InputPlaceholder { get; set; } = string.Empty;
    public string ResultLabel { get; set; } = "Sonuç";
    public List<FieldOption> Options { get; set; } = new();
}

public class FieldOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class CalculateRequest
{
    [Required]
    public string ServiceType { get; set; } = string.Empty;

    [Required]
    public string SubCategory { get; set; } = string.Empty;

    public string? InputValue { get; set; }

    public string? Direction { get; set; }

    public string? City { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }
}

/// <summary>Coordinates to resolve to a city and district.</summary>
public sealed record ResolveLocationRequest
{
    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }
}

/// <summary>A city and district resolved from geographic coordinates.</summary>
public sealed record ResolvedLocationResponse(string City, string District);

public class CalculateResponse
{
    public bool Success { get; set; }
    public string ServiceType { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public string? InputLabel { get; set; }
    public string? InputValue { get; set; }
    public string? ResultLabel { get; set; }
    public string? ResultValue { get; set; }
    public string? RawDetail { get; set; }
    public bool SavedToHistory { get; set; }
    public string? Message { get; set; }
}

public class UserActivityDto
{
    public int Id { get; set; }
    public string ServiceType { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public string? InputLabel { get; set; }
    public string? InputValue { get; set; }
    public string? ResultLabel { get; set; }
    public string? ResultValue { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class SharedApiDataDto
{
    public int Id { get; set; }
    public string DataType { get; set; } = string.Empty;
    public string RecordKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateOnly DataDate { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime FetchedAtUtc { get; set; }
    public bool IsAdminManaged { get; set; }
}

public sealed class UpsertSharedApiDataRequest
{
    [Required]
    [MaxLength(32)]
    public string DataType { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string RecordKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    public DateOnly DataDate { get; set; }

    [Required]
    [MaxLength(65536)]
    public string PayloadJson { get; set; } = string.Empty;
}
