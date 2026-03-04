using Microsoft.EntityFrameworkCore;
using SaaSPlatform.Application.DTOs;
using SaaSPlatform.Application.Interfaces;
using SaaSPlatform.Domain.Entities;
using SaaSPlatform.Infrastructure.Persistence;
using System.Security.Cryptography;

namespace SaaSPlatform.Infrastructure.Security;

public class ApiKeyService : IApiKeyService
{
    private readonly AppDbContext _db;

    public ApiKeyService(AppDbContext db) => _db = db;

    public async Task<CreateApiKeyResponse> GenerateApiKeyAsync(Guid organizationId, CreateApiKeyRequest request, CancellationToken ct)
    {
        // Generate a 32-byte secure random key
        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        var base64Key = Convert.ToBase64String(keyBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('='); // URL-safe Base64

        // Prefix + full key forms the plaintext string we give the user
        var prefix = "sk_live_" + base64Key[..8];
        var plaintextKey = $"{prefix}.{base64Key}";

        // Hash it securely using BCrypt
        var keyHash = BCrypt.Net.BCrypt.HashPassword(plaintextKey);

        var apiKey = new ApiKey
        {
            OrganizationId = organizationId,
            Name = request.Name,
            Prefix = prefix,
            KeyHash = keyHash
        };

        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync(ct);

        return new CreateApiKeyResponse
        {
            Id = apiKey.Id,
            Name = apiKey.Name,
            Token = plaintextKey, // Returned exactly once
            CreatedAt = apiKey.CreatedAt
        };
    }

    public async Task<List<ApiKeyResponse>> GetApiKeysAsync(Guid organizationId, CancellationToken ct)
    {
        return await _db.ApiKeys
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ApiKeyResponse
            {
                Id = x.Id,
                Name = x.Name,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                ExpiresAt = x.ExpiresAt,
                LastUsedAt = x.LastUsedAt
            })
            .ToListAsync(ct);
    }

    public async Task<bool> RevokeApiKeyAsync(Guid organizationId, Guid apiKeyId, CancellationToken ct)
    {
        var key = await _db.ApiKeys.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == apiKeyId, ct);
        if (key == null) return false;

        key.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
