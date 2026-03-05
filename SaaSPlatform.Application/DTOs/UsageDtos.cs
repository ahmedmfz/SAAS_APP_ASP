using System.ComponentModel.DataAnnotations;

namespace SaaSPlatform.Application.DTOs;

public class RecordUsageRequest
{
    [Required(ErrorMessage = "Endpoint is required.")]
    [StringLength(200, ErrorMessage = "Endpoint cannot exceed 200 characters.")]
    public string Endpoint { get; set; } = null!;

    [Required(ErrorMessage = "StatusCode is required")]
    [Range(100, 599, ErrorMessage = "StatusCode must be a valid HTTP status code.")]
    public int StatusCode { get; set; }

    // Optional idempotency key to prevent double requests for the exact same event
    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }
}

public class UsageRecordResponse
{
    public long Id { get; set; }
    public Guid ApiKeyId { get; set; }
    public string Endpoint { get; set; } = default!;
    public int StatusCode { get; set; }
    public DateTime OccurredAt { get; set; }
}
