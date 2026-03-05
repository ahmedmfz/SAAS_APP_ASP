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
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<OrganizationUsageMonthly> OrganizationUsageMonthly => Set<OrganizationUsageMonthly>();
    public DbSet<UserUsageMonthly> UserUsageMonthly => Set<UserUsageMonthly>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ────────────────────────────────────────────────────────
        modelBuilder.Entity<User>()
            .HasOne(u => u.Organization)
            .WithMany(o => o.Users)
            .HasForeignKey(u => u.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // ── OrganizationSubscription ─────────────────────────────────────
        modelBuilder.Entity<OrganizationSubscription>()
            .HasIndex(x => new { x.OrganizationId, x.PlanId });

        // ── ApiKey ───────────────────────────────────────────────────────
        modelBuilder.Entity<ApiKey>()
            .HasOne(k => k.Organization)
            .WithMany()
            .HasForeignKey(k => k.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ApiKey>()
            .HasOne(k => k.User)
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        // Fast lookup by prefix during authentication
        modelBuilder.Entity<ApiKey>()
            .HasIndex(k => k.Prefix);

        // ── UsageRecord ──────────────────────────────────────────────────
        modelBuilder.Entity<UsageRecord>()
            .HasOne(r => r.Organization)
            .WithMany()
            .HasForeignKey(r => r.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UsageRecord>()
            .HasOne(r => r.ApiKey)
            .WithMany()
            .HasForeignKey(r => r.ApiKeyId)
            .OnDelete(DeleteBehavior.Restrict); // don't cascade-delete usage history

        // Composite index for analytics: "all calls by org in date range"
        modelBuilder.Entity<UsageRecord>()
            .HasIndex(r => new { r.OrganizationId, r.OccurredAt });

        // ── OrganizationUsageMonthly ─────────────────────────────────────
        modelBuilder.Entity<OrganizationUsageMonthly>()
            .HasOne(m => m.Organization)
            .WithMany()
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: only ONE row per org per month
        modelBuilder.Entity<OrganizationUsageMonthly>()
            .HasIndex(m => new { m.OrganizationId, m.YearMonth })
            .IsUnique();

        // EF Core optimistic concurrency
        modelBuilder.Entity<OrganizationUsageMonthly>()
            .Property(m => m.RowVersion)
            .IsConcurrencyToken();

        // ── UserUsageMonthly ─────────────────────────────────────────────
        modelBuilder.Entity<UserUsageMonthly>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserUsageMonthly>()
            .HasOne(m => m.Organization)
            .WithMany()
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.NoAction);

        // Unique: one row per user per month
        modelBuilder.Entity<UserUsageMonthly>()
            .HasIndex(m => new { m.UserId, m.YearMonth })
            .IsUnique();

        modelBuilder.Entity<UserUsageMonthly>()
            .Property(m => m.RowVersion)
            .IsConcurrencyToken();
    }
}