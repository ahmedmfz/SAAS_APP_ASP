namespace SaaSPlatform.Domain.Entities;

public class OrganizationUsageMonthly
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    /// <summary>Format: YYYYMM — e.g. 202503 for March 2025</summary>
    public int YearMonth { get; set; }

    public long ApiCallCount { get; set; } = 0;

    /// <summary>
    /// RowVersion / concurrency token — EF Core uses this to prevent
    /// two requests from incrementing the counter at the same time (optimistic concurrency).
    /// Same concept as a DB timestamp column.
    /// </summary>
    public uint RowVersion { get; set; }

    // Navigation
    public Organization Organization { get; set; } = default!;
}
