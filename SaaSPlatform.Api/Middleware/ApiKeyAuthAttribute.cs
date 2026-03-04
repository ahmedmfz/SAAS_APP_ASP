using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SaaSPlatform.Application.Common;
using SaaSPlatform.Infrastructure.Persistence;

namespace SaaSPlatform.Api.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAuthAttribute : Attribute, IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Fail("API Key was not provided in X-Api-Key headers."));
            return;
        }

        var incomingKey = extractedApiKey.ToString();
        var parts = incomingKey.Split('.');
        
        // Ensure format is: sk_live_prefix.base64key (2 parts)
        if (parts.Length != 2)
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Fail("Invalid API Key format."));
            return;
        }

        var prefix = parts[0];

        // Retrieve DbContext from Request Services
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        // Find candidate keys purely by prefix (avoids full table scan)
        var candidates = await db.ApiKeys
            .Where(x => x.Prefix == prefix && x.IsActive)
            .AsNoTracking() // Just reading for auth
            .ToListAsync();

        if (candidates.Count == 0)
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Fail("API Key is invalid or deactivated."));
            return;
        }

        // Verify Hash against BCrypt
        var matchedKey = candidates.FirstOrDefault(k => BCrypt.Net.BCrypt.Verify(incomingKey, k.KeyHash));
        
        if (matchedKey == null)
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Fail("API Key is invalid."));
            return;
        }

        // Store identity downstream into HttpContext
        context.HttpContext.Items["OrganizationId"] = matchedKey.OrganizationId;
        context.HttpContext.Items["ApiKeyId"] = matchedKey.Id;

        await next(); // Proceed to Controller
    }
}
