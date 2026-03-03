namespace SaaSPlatform.Application.DTOs;


public record RegisterRequest(string OrganizationName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string AccessToken, Guid UserId, Guid OrganizationId, string Role);