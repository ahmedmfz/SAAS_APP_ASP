using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSPlatform.Application.Common;
using SaaSPlatform.Application.DTOs;
using SaaSPlatform.Application.Interfaces;

namespace SaaSPlatform.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")] // Satisfies "Only Admin users can access this endpoint"
public class AdminController : ControllerBase
{
    private readonly IUsageService _usageService;

    public AdminController(IUsageService usageService)
    {
        _usageService = usageService;
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetAnalytics([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        if (from == default || to == default)
        {
            return BadRequest(ApiResponse<object>.Fail("Both 'from' and 'to' dates must be provided."));
        }

        if (from > to)
        {
            return BadRequest(ApiResponse<object>.Fail("'from' date cannot be after 'to' date."));
        }

        // Extract organization from the authenticated Admin's JWT
        var orgIdClaim = User.Claims.FirstOrDefault(c => c.Type == "orgId")?.Value;
        if (string.IsNullOrEmpty(orgIdClaim) || !Guid.TryParse(orgIdClaim, out var orgId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid organization mapping in token."));
        }

        var analytics = await _usageService.GetUsageAnalyticsAsync(orgId, from, to, ct);
        
        return Ok(ApiResponse<UsageAnalyticsResponse>.Ok(analytics, "Usage analytics retrieved successfully."));
    }
}
