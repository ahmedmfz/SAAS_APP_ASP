namespace SaaSPlatform.Domain.Entities;

/// <summary>
/// Tracks how many API calls a single user has made this calendar month.
/// Enforces the per-user limit defined on the SubscriptionPlan.
/// </summary>
public class UserUsageMonthly
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>Format: YYYYMM — e.g. 202503 for March 2025</summary>
    public int YearMonth { get; set; }

    public long ApiCallCount { get; set; } = 0;

    public uint RowVersion { get; set; }

    // Navigation
    public User User { get; set; } = default!;
    public Organization Organization { get; set; } = default!;
}
