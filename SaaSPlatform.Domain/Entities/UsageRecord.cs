namespace SaaSPlatform.Domain.Entities;

public class UsageRecord
{
    public long Id { get; set; }                        // long for large volume

    public Guid OrganizationId { get; set; }
    public Guid ApiKeyId { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string Endpoint { get; set; } = default!;   // e.g. "POST /api/data"
    public int StatusCode { get; set; }                 // 200, 400, 429 etc.

    // Navigation
    public Organization Organization { get; set; } = default!;
    public ApiKey ApiKey { get; set; } = default!;
}
