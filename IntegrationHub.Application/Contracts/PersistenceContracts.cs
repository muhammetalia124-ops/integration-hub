using IntegrationHub.Models.Entities;

namespace IntegrationHub.Application.Contracts;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync(CancellationToken ct);
    Task<User?> GetByIdAsync(int id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<bool> EmailExistsAsync(string email, int? exceptUserId, CancellationToken ct);
    Task<bool> AnyAdminAsync(CancellationToken ct);
    Task<int> CountActiveAdminsAsync(CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    void Remove(User user);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IUserActivityRepository
{
    Task<List<UserActivity>> GetHistoryAsync(int userId, int take, CancellationToken ct);
    Task<UserActivity?> FindLatestMatchAsync(
        int userId,
        string serviceType,
        string subCategory,
        string? inputValue,
        string? resultValue,
        CancellationToken ct);
    Task AddAsync(UserActivity activity, CancellationToken ct);
    Task DeleteByUserAsync(int userId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IPrayerTimeCacheRepository
{
    Task<PrayerTimeCache?> GetAsync(string districtId, DateOnly prayerDate, CancellationToken ct);
    Task UpsertRangeAsync(IReadOnlyCollection<PrayerTimeCache> entries, CancellationToken ct);
}

public interface ICurrencyRateRepository
{
    Task<List<CurrencyRate>> GetAllAsync(CancellationToken ct);
    Task ReplaceAllAsync(IReadOnlyCollection<CurrencyRate> rates, CancellationToken ct);
}

public interface IExternalApiCacheRepository
{
    Task<ExternalApiCacheEntry?> GetValidAsync(string cacheKey, DateTime nowUtc, CancellationToken ct);
    Task StoreAsync(ExternalApiCacheEntry entry, CancellationToken ct);
}

public interface ISharedApiDataRepository
{
    Task<List<SharedApiDataRecord>> GetListAsync(
        string? dataType,
        DateOnly? from,
        DateOnly? to,
        int take,
        CancellationToken ct);
    Task<SharedApiDataRecord?> GetByIdAsync(int id, CancellationToken ct);
    Task<SharedApiDataRecord?> GetAdminManagedDailyAsync(
        string dataType,
        string recordKey,
        DateOnly dataDate,
        CancellationToken ct);
    Task<bool> ExistsDailyAsync(
        string dataType,
        string recordKey,
        DateOnly dataDate,
        int exceptId,
        CancellationToken ct);
    Task UpsertDailyAsync(SharedApiDataRecord record, CancellationToken ct);
    Task UpsertDailyRangeAsync(IReadOnlyCollection<SharedApiDataRecord> records, CancellationToken ct);
    Task AddAsync(SharedApiDataRecord record, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IExternalApiCache
{
    Task<T?> GetOrCreateAsync<T>(
        string cacheKey,
        TimeSpan lifetime,
        Func<CancellationToken, Task<T?>> factory,
        Func<T, bool> shouldCache,
        CancellationToken ct)
        where T : class;
}

public interface IExchangeRateProvider
{
    Task<IReadOnlyDictionary<string, CurrencyRate>> GetRatesAsync(CancellationToken ct);
}

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}
