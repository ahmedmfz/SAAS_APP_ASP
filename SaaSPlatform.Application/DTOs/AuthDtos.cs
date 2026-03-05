using System.ComponentModel.DataAnnotations;

namespace SaaSPlatform.Application.DTOs;

public record CreateUserRequest
{
    [Required]
    public Guid OrganizationId { get; init; }

    [Required]
    [EmailAddress]
    public string Email { get; init; } = default!;

    [Required]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; init; } = default!;

    /// <summary>1 = Admin, 2 = Member</summary>
    [Required]
    [Range(1, 2, ErrorMessage = "Role must be 1 (Admin) or 2 (Member).")]
    public int Role { get; init; }
}

public record LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = default!;

    [Required]
    public string Password { get; init; } = default!;
}

public record AuthResponse(string AccessToken, Guid UserId, Guid OrganizationId, string Role);