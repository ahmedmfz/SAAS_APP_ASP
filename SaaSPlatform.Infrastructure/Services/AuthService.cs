using Microsoft.AspNetCore.Identity;
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
    private readonly PasswordHasher<User> _hasher = new();

    public AuthService(AppDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(x => x.Email == email, ct);
        if (exists) throw new InvalidOperationException("Email already registered.");

        var org = new Organization { Name = req.OrganizationName.Trim(), Status = OrganizationStatus.Active };
        var user = new User
        {
            OrganizationId = org.Id,
            Email = email,
            Role = UserRole.Admin
        };
        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        // Assumption: new org starts on Basic plan for 30 days trial
        var sub = new OrganizationSubscription
        {
            OrganizationId = org.Id,
            PlanId = 1,
            StartAt = DateTime.UtcNow,
            EndAt = DateTime.UtcNow.AddDays(30)
        };

        _db.Organizations.Add(org);
        _db.Users.Add(user);
        _db.OrganizationSubscriptions.Add(sub);

        await _db.SaveChangesAsync(ct);

        var token = _jwt.CreateToken(user);
        return new AuthResponse(token, user.Id, user.OrganizationId, user.Role.ToString());
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email, ct);
        if (user is null) throw new UnauthorizedAccessException("Invalid credentials.");

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (verify == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var token = _jwt.CreateToken(user);
        return new AuthResponse(token, user.Id, user.OrganizationId, user.Role.ToString());
    }
}