using Microsoft.EntityFrameworkCore;
using SaaSPlatform.Domain.Entities;

namespace SaaSPlatform.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<OrganizationSubscription> OrganizationSubscriptions => Set<OrganizationSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<OrganizationSubscription>()
            .HasIndex(x => new { x.OrganizationId, x.PlanId });

        modelBuilder.Entity<SubscriptionPlan>().HasData(
            new SubscriptionPlan { Id = 1, Name = "Basic", ApiCallsPerMonth = 10_000, StorageLimitMb = 500 },
            new SubscriptionPlan { Id = 2, Name = "Pro", ApiCallsPerMonth = 100_000, StorageLimitMb = 5_000 }
        );
    }
}