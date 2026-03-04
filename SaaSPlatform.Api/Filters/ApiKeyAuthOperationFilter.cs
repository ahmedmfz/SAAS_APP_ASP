using Microsoft.OpenApi.Models;
using SaaSPlatform.Api.Middleware;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SaaSPlatform.Api.Filters;

/// <summary>
/// Swagger operation filter that adds the ApiKey security requirement
/// ONLY to endpoints decorated with [ApiKeyAuth], not globally.
/// </summary>
public class ApiKeyAuthOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasApiKeyAuth =
            context.MethodInfo.DeclaringType!.GetCustomAttributes(true).OfType<ApiKeyAuthAttribute>().Any() ||
            context.MethodInfo.GetCustomAttributes(true).OfType<ApiKeyAuthAttribute>().Any();

        if (!hasApiKeyAuth) return;

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id   = "ApiKey"
                    }
                },
                Array.Empty<string>()
            }
        });
    }
}
