using Microsoft.EntityFrameworkCore;
using SaaSPlatform.Application.DTOs;
using SaaSPlatform.Application.Interfaces;
using SaaSPlatform.Domain.Entities;
using SaaSPlatform.Domain.Enums;
using SaaSPlatform.Infrastructure.Persistence;

namespace SaaSPlatform.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;

    public AuthService(AppDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        // Check email not already taken
        var exists = await _db.Users.AnyAsync(x => x.Email == email, ct);
        if (exists) throw new InvalidOperationException("Email already registered.");

        // Validate the organization exists (since it's seeded)
        var org = await _db.Organizations.FindAsync(new object[] { req.OrganizationId }, ct);
        if (org is null) throw new InvalidOperationException("Organization not found.");

        // Create the user as an Admin for this seeded organization
        var user = new User
        {
            OrganizationId = org.Id,
            Email = email,
            Role = UserRole.Admin, // Since org is pre-made, they represent the org Admin
            Password = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };
        _db.Users.Add(user);

        await _db.SaveChangesAsync(ct);

        var token = _jwt.CreateToken(user);
        return new AuthResponse(token, user.Id, user.OrganizationId, user.Role.ToString());
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email, ct);
        if (user is null) throw new UnauthorizedAccessException("Invalid credentials.");

        var valid = BCrypt.Net.BCrypt.Verify(req.Password, user.Password);
        if (!valid) throw new UnauthorizedAccessException("Invalid credentials.");

        var token = _jwt.CreateToken(user);
        return new AuthResponse(token, user.Id, user.OrganizationId, user.Role.ToString());
    }
}