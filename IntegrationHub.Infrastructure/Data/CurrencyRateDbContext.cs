using IntegrationHub.Data.Configurations;
using IntegrationHub.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Data;

public sealed class CurrencyRateDbContext : DbContext
{
    public CurrencyRateDbContext(DbContextOptions<CurrencyRateDbContext> options) : base(options) { }

    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
    public DbSet<PrayerTimeCache> PrayerTimeCache => Set<PrayerTimeCache>();
    public DbSet<ExternalApiCacheEntry> ExternalApiCache => Set<ExternalApiCacheEntry>();
    public DbSet<SharedApiDataRecord> SharedApiData => Set<SharedApiDataRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CurrencyRateConfiguration());
        modelBuilder.ApplyConfiguration(new PrayerTimeCacheConfiguration());
        modelBuilder.ApplyConfiguration(new ExternalApiCacheConfiguration());
        modelBuilder.ApplyConfiguration(new SharedApiDataConfiguration());
    }
}
