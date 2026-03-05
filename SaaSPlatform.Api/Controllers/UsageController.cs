using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSPlatform.Api.Middleware;
using SaaSPlatform.Application.Common;
using SaaSPlatform.Application.DTOs;
using SaaSPlatform.Application.Interfaces;

namespace SaaSPlatform.Api.Controllers;

[ApiController]
[Route("api/usage")]
[Authorize] // Default: use standard JWT authentication
public class UsageController : ControllerBase
{
    private readonly IUsageService _usageService;

    public UsageController(IUsageService usageService)
    {
        _usageService = usageService;
    }

    [HttpPost("record")]
    [AllowAnonymous] // Override class-level Authorize
    [ApiKeyAuth]     // Custom filter handles the authentication here instead
    public async Task<IActionResult> RecordUsage([FromBody] RecordUsageRequest request, CancellationToken ct)
    {
        // OrganizationId and ApiKeyId are injected into HttpContext by ApiKeyAuthAttribute
        var orgId = (Guid)HttpContext.Items["OrganizationId"]!;
        var apiKeyId = (Guid)HttpContext.Items["ApiKeyId"]!;

        var success = await _usageService.RecordUsageAsync(orgId, apiKeyId, request, ct);
        return Ok(ApiResponse<object>.Ok(new { success }, "Usage log recorded successfully."));
    }

    [HttpGet("records")]
    public async Task<IActionResult> GetUsageRecords(CancellationToken ct)
    {
        // Standard JWT Claims Extraction
        var orgIdClaim = User.Claims.FirstOrDefault(c => c.Type == "orgId")?.Value;
        if (!Guid.TryParse(orgIdClaim, out var orgId)) return Unauthorized();

        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
        Guid? userId = Guid.TryParse(userIdClaim, out var uid) ? uid : null;

        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
        if (!Enum.TryParse<SaaSPlatform.Domain.Enums.UserRole>(roleClaim, true, out var role))
        {
            role = SaaSPlatform.Domain.Enums.UserRole.Member; // Default safe fallback
        }

        var records = await _usageService.GetUsageRecordsAsync(orgId, userId, role, ct);
        return Ok(ApiResponse<List<UsageRecordResponse>>.Ok(records, "Usage records retrieved."));
    }
}
