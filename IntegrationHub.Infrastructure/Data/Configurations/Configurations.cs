using IntegrationHub.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "Auth");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.IsAdmin).IsRequired();
    }
}

public class UserActivityConfiguration : IEntityTypeConfiguration<UserActivity>
{
    public void Configure(EntityTypeBuilder<UserActivity> builder)
    {
        builder.ToTable("UserActivities", "Activity");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.ServiceType).IsRequired().HasMaxLength(64);
        builder.Property(a => a.SubCategory).IsRequired().HasMaxLength(64);
        builder.Property(a => a.InputLabel).HasMaxLength(128);
        builder.Property(a => a.InputValue).HasMaxLength(256);
        builder.Property(a => a.ResultLabel).HasMaxLength(128);
        builder.Property(a => a.ResultValue).HasMaxLength(512);
        builder.Property(a => a.Timestamp).IsRequired();

        builder.HasIndex(a => new { a.UserId, a.ServiceType, a.SubCategory })
            .HasDatabaseName("IX_UserActivities_Lookup");

        builder.HasIndex(a => a.Timestamp)
            .HasDatabaseName("IX_UserActivities_Timestamp");
    }
}

public class PrayerTimeCacheConfiguration : IEntityTypeConfiguration<PrayerTimeCache>
{
    public void Configure(EntityTypeBuilder<PrayerTimeCache> builder)
    {
        builder.ToTable("PrayerTimeCache", "Cache");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.DistrictId).IsRequired().HasMaxLength(32);
        builder.Property(p => p.City).IsRequired().HasMaxLength(128);
        builder.Property(p => p.District).IsRequired().HasMaxLength(128);
        builder.Property(p => p.PrayerDate).IsRequired().HasColumnType("date");
        builder.Property(p => p.Imsak).IsRequired().HasMaxLength(5);
        builder.Property(p => p.Gunes).IsRequired().HasMaxLength(5);
        builder.Property(p => p.Ogle).IsRequired().HasMaxLength(5);
        builder.Property(p => p.Ikindi).IsRequired().HasMaxLength(5);
        builder.Property(p => p.Aksam).IsRequired().HasMaxLength(5);
        builder.Property(p => p.Yatsi).IsRequired().HasMaxLength(5);
        builder.Property(p => p.FetchedAtUtc).IsRequired();

        builder.HasIndex(p => new { p.DistrictId, p.PrayerDate })
            .IsUnique()
            .HasDatabaseName("IX_PrayerTimeCache_District_Date");
    }
}

public sealed class CurrencyRateConfiguration : IEntityTypeConfiguration<CurrencyRate>
{
    public void Configure(EntityTypeBuilder<CurrencyRate> builder)
    {
        builder.ToTable("CurrencyRates", "Cache");

        builder.HasKey(rate => rate.Code);
        builder.Property(rate => rate.Code).HasMaxLength(3).IsRequired();
        builder.Property(rate => rate.Name).HasMaxLength(128).IsRequired();
        builder.Property(rate => rate.ForexBuying).HasPrecision(18, 8).IsRequired();
        builder.Property(rate => rate.ForexSelling).HasPrecision(18, 8).IsRequired();
        builder.Property(rate => rate.FetchedAtUtc).IsRequired();
    }
}

public sealed class ExternalApiCacheConfiguration : IEntityTypeConfiguration<ExternalApiCacheEntry>
{
    public void Configure(EntityTypeBuilder<ExternalApiCacheEntry> builder)
    {
        builder.ToTable("ExternalApiCache", "Cache");

        builder.HasKey(entry => entry.CacheKey);
        builder.Property(entry => entry.CacheKey).HasMaxLength(256).IsRequired();
        builder.Property(entry => entry.PayloadJson).IsRequired();
        builder.Property(entry => entry.FetchedAtUtc).IsRequired();
        builder.Property(entry => entry.ExpiresAtUtc).IsRequired();

        builder.HasIndex(entry => entry.ExpiresAtUtc)
            .HasDatabaseName("IX_ExternalApiCache_ExpiresAtUtc");
    }
}

public sealed class SharedApiDataConfiguration : IEntityTypeConfiguration<SharedApiDataRecord>
{
    public void Configure(EntityTypeBuilder<SharedApiDataRecord> builder)
    {
        builder.ToTable("SharedApiData", "Cache");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.DataType).IsRequired().HasMaxLength(32);
        builder.Property(record => record.RecordKey).IsRequired().HasMaxLength(256);
        builder.Property(record => record.DisplayName).IsRequired().HasMaxLength(128);
        builder.Property(record => record.DataDate).IsRequired().HasColumnType("date");
        builder.Property(record => record.PayloadJson).IsRequired();
        builder.Property(record => record.FetchedAtUtc).IsRequired();
        builder.Property(record => record.IsAdminManaged).IsRequired().HasDefaultValue(false);
        builder.Property(record => record.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasIndex(record => new { record.DataType, record.RecordKey, record.DataDate })
            .IsUnique()
            .HasDatabaseName("IX_SharedApiData_Type_Key_Date");
        builder.HasIndex(record => new { record.DataType, record.DataDate })
            .HasDatabaseName("IX_SharedApiData_Type_Date");
    }
}
