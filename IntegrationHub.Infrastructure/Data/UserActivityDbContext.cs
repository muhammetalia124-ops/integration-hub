using IntegrationHub.Data.Configurations;
using IntegrationHub.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Data;

public class UserActivityDbContext : DbContext
{
    public UserActivityDbContext(DbContextOptions<UserActivityDbContext> options) : base(options) { }

    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserActivityConfiguration());
    }
}
