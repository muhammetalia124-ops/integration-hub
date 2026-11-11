using IntegrationHub.Application.Contracts;
using IntegrationHub.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Data.Repositories;

public sealed class EfUserRepository : IUserRepository
{
    private readonly UserDbContext _db;

    public EfUserRepository(UserDbContext db)
    {
        _db = db;
    }

    public Task<List<User>> GetAllAsync(CancellationToken ct) =>
        _db.Users.OrderBy(user => user.Id).ToListAsync(ct);

    public Task<User?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.Users.FirstOrDefaultAsync(user => user.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        _db.Users.FirstOrDefaultAsync(user => user.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, int? exceptUserId, CancellationToken ct) =>
        _db.Users.AnyAsync(
            user => user.Email == email && (!exceptUserId.HasValue || user.Id != exceptUserId.Value),
            ct);

    public Task<bool> AnyAdminAsync(CancellationToken ct) =>
        _db.Users.AnyAsync(user => user.IsAdmin, ct);

    public Task<int> CountActiveAdminsAsync(CancellationToken ct) =>
        _db.Users.CountAsync(user => user.IsAdmin && user.IsActive, ct);

    public async Task AddAsync(User user, CancellationToken ct)
    {
        await _db.Users.AddAsync(user, ct);
        await _db.SaveChangesAsync(ct);
    }

    public void Remove(User user) => _db.Users.Remove(user);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
