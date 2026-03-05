using SaaSPlatform.Application.DTOs;

namespace SaaSPlatform.Application.Interfaces;

public interface IApiKeyService
{
    Task<CreateApiKeyResponse> GenerateApiKeyAsync(Guid organizationId, Guid? userId, CreateApiKeyRequest request, CancellationToken ct);
    Task<List<ApiKeyResponse>> GetApiKeysAsync(Guid organizationId, CancellationToken ct);
    Task<bool> RevokeApiKeyAsync(Guid organizationId, Guid apiKeyId, CancellationToken ct);
}
