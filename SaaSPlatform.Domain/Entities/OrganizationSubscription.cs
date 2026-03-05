namespace SaaSPlatform.Domain.Entities;

public class OrganizationSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public int PlanId { get; set; }

    // Snapshot of limits at the time of subscription purchase
    public int ApiCallsMonthly { get; set; }
    public int ApiCallsPerUser { get; set; }

    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
}