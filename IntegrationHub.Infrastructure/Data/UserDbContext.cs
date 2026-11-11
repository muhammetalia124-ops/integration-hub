using IntegrationHub.Data.Configurations;
using IntegrationHub.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Data;

public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
    }
}
