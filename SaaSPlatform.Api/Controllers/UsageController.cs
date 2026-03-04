using Microsoft.AspNetCore.Mvc;
using SaaSPlatform.Api.Middleware;
using SaaSPlatform.Application.Common;
using SaaSPlatform.Application.DTOs;
using SaaSPlatform.Application.Interfaces;

namespace SaaSPlatform.Api.Controllers;

[ApiController]
[Route("api/usage")]
[ApiKeyAuth] // Uses the custom Api Key Auth Filter instead of JWT!
public class UsageController : ControllerBase
{
    private readonly IUsageService _usageService;

    public UsageController(IUsageService usageService) => _usageService = usageService;

    [HttpPost("record")]
    public async Task<IActionResult> RecordUsage([FromBody] RecordUsageRequest request, CancellationToken ct)
    {
        // OrganizationId and ApiKeyId are injected into HttpContext by ApiKeyAuthAttribute
        var orgId = (Guid)HttpContext.Items["OrganizationId"]!;
        var apiKeyId = (Guid)HttpContext.Items["ApiKeyId"]!;

        var success = await _usageService.RecordUsageAsync(orgId, apiKeyId, request, ct);
        return Ok(ApiResponse<object>.Ok(new { success }, "Usage log recorded successfully."));
    }
}
