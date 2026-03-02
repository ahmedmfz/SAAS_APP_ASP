namespace SaaSPlatform.Domain.Entities;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public int ApiCallsPerMonth { get; set; }
    public int StorageLimitMb { get; set; }
}
