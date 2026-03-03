using SaaSPlatform.Domain.Enums;

namespace SaaSPlatform.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.Member;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    
    // Navigation property: Many Users belong to One Organization
    public Organization Organization { get; set; } = default!;}