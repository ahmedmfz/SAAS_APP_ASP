using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSPlatform.Application.Common;
using SaaSPlatform.Application.DTOs;
using SaaSPlatform.Application.Interfaces;

namespace SaaSPlatform.Api.Controllers;

[ApiController]
[Route("api/apikeys")]
[Authorize(Policy = "AdminOnly")] // Only authenticated Admins can manage keys
public class ApiKeysController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;

    public ApiKeysController(IApiKeyService apiKeyService) => _apiKeyService = apiKeyService;

    [HttpPost]
    public async Task<IActionResult> GenerateKey([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        // Extract orgId from the JWT Claims
        var orgIdClaim = User.Claims.FirstOrDefault(c => c.Type == "orgId")?.Value;
        if (!Guid.TryParse(orgIdClaim, out var orgId)) return Unauthorized();

        var result = await _apiKeyService.GenerateApiKeyAsync(orgId, request, ct);
        return Ok(ApiResponse<CreateApiKeyResponse>.Ok(result, "API Key generated securely. Warning: Please store the PlaintextKey now, it will not be shown again."));
    }

    [HttpGet]
    public async Task<IActionResult> GetKeys(CancellationToken ct)
    {
        var orgIdClaim = User.Claims.FirstOrDefault(c => c.Type == "orgId")?.Value;
        if (!Guid.TryParse(orgIdClaim, out var orgId)) return Unauthorized();

        var result = await _apiKeyService.GetApiKeysAsync(orgId, ct);
        return Ok(ApiResponse<List<ApiKeyResponse>>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeKey(Guid id, CancellationToken ct)
    {
        var orgIdClaim = User.Claims.FirstOrDefault(c => c.Type == "orgId")?.Value;
        if (!Guid.TryParse(orgIdClaim, out var orgId)) return Unauthorized();

        var success = await _apiKeyService.RevokeApiKeyAsync(orgId, id, ct);
        if (!success) return NotFound(ApiResponse<object>.Fail("API Key not found or already revoked."));

        return Ok(ApiResponse<object>.Ok(null, "API Key revoked successfully."));
    }
}
