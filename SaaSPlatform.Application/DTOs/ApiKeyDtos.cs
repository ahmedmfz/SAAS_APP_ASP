using System.ComponentModel.DataAnnotations;

namespace SaaSPlatform.Application.DTOs;

public class CreateApiKeyRequest
{
    [Required(ErrorMessage = "Name is required for the API Key.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Key Name must be between 2 and 50 characters.")]
    public string Name { get; set; } = null!;
}

public class ApiKeyResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class CreateApiKeyResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Token { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
