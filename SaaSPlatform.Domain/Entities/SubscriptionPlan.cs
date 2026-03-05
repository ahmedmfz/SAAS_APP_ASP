namespace SaaSPlatform.Domain.Entities;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public int ApiCallsPerMonth { get; set; }   // Org-wide monthly limit
    public int ApiCallsPerUser { get; set; }    // Per-user monthly limit
    public int StorageLimitMb { get; set; }
}
