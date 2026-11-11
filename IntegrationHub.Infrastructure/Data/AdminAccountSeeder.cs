using IntegrationHub.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Data;

public static class AdminAccountSeeder
{
    public static async Task SeedAsync(
        UserDbContext db,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(user => user.IsAdmin, cancellationToken))
        {
            return;
        }

        var email = configuration["AdminBootstrap:Email"];
        var username = configuration["AdminBootstrap:Username"];
        var password = configuration["AdminBootstrap:Password"];
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("İlk yönetici hesabı için AdminBootstrap ayarları eksik.");
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                Email = email,
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            db.Users.Add(user);
        }

        user.IsAdmin = true;
        user.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("İlk yönetici hesabı hazırlandı: {Email}", user.Email);
    }
}
