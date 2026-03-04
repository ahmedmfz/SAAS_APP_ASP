using Microsoft.EntityFrameworkCore;
using SaaSPlatform.Domain.Entities;
using SaaSPlatform.Domain.Enums;

namespace SaaSPlatform.Infrastructure.Persistence;

/// <summary>
/// Laravel-style seeder — runs at startup, idempotent (safe to run multiple times).
/// Add new seed methods here following the same pattern.
/// </summary>
public static class DatabaseSeeder
{
    // Default seeded credentials — for testing only
    private static readonly Guid AcmeOrgId = new("11111111-1111-1111-1111-111111111111");
    private const string AdminEmail    = "admin@acme.com";
    private const string AdminPassword = "Admin@1234";  // BCrypt hashed below

    public static async Task SeedAsync(AppDbContext db)
    {
        await SeedOrganizationsAsync(db);
        await SeedSubscriptionPlansAsync(db);
        await SeedUsersAsync(db);
        await SeedOrganizationSubscriptionsAsync(db);
    }

    // ------------------------------------------------------------------ //
    //  Organizations
    // ------------------------------------------------------------------ //
    private static async Task SeedOrganizationsAsync(AppDbContext db)
    {
        // Idempotent: only insert if the table is empty
        if (await db.Organizations.AnyAsync()) return;

        var orgs = new List<Organization>
        {
            new() { Id = new Guid("11111111-1111-1111-1111-111111111111"), Name = "Acme Corp", Status = OrganizationStatus.Active },
            new() { Id = new Guid("22222222-2222-2222-2222-222222222222"), Name = "Beta Inc",  Status = OrganizationStatus.Active },
        };

        db.Organizations.AddRange(orgs);
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ //
    //  Subscription Plans
    // ------------------------------------------------------------------ //
    private static async Task SeedSubscriptionPlansAsync(AppDbContext db)
    {
        if (await db.SubscriptionPlans.AnyAsync()) return;

        var plans = new List<SubscriptionPlan>
        {
            new() { Name = "Basic", ApiCallsPerMonth = 10_000,  StorageLimitMb = 500   },
            new() { Name = "Pro",   ApiCallsPerMonth = 100_000, StorageLimitMb = 5_000 },
        };

        db.SubscriptionPlans.AddRange(plans);
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ //
    //  Users
    // ------------------------------------------------------------------ //
    private static async Task SeedUsersAsync(AppDbContext db)
    {
        // Idempotent: only insert if admin user doesn't exist yet
        if (await db.Users.AnyAsync(u => u.Email == AdminEmail)) return;

        var adminUser = new User
        {
            Email          = AdminEmail,
            Password       = BCrypt.Net.BCrypt.HashPassword(AdminPassword),
            OrganizationId = AcmeOrgId,
            Role           = UserRole.Admin
        };

        db.Users.Add(adminUser);
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ //
    //  Organization Subscriptions
    // ------------------------------------------------------------------ //
    private static async Task SeedOrganizationSubscriptionsAsync(AppDbContext db)
    {
        // Check if there's already an active subscription for Acme Org
        var now = DateTime.UtcNow;
        if (await db.OrganizationSubscriptions.AnyAsync(s => s.OrganizationId == AcmeOrgId && s.EndAt >= now))
            return;

        // Get the Pro plan ID
        var proPlan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Name == "Pro");
        if (proPlan == null) return;

        var subscription = new OrganizationSubscription
        {
            OrganizationId = AcmeOrgId,
            PlanId         = proPlan.Id,
            StartAt        = now.AddDays(-1),
            EndAt          = now.AddYears(1)
        };

        db.OrganizationSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
    }
}
