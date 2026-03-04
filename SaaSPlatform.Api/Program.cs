using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SaaSPlatform.Api.Middleware;
using SaaSPlatform.Application.Common;
using SaaSPlatform.Application.Interfaces;
using SaaSPlatform.Infrastructure.Persistence;
using SaaSPlatform.Infrastructure.Security;
using SaaSPlatform.Infrastructure.Services;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Controllers + override default 400 validation response → 422 with unified format
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(opt =>
{
    opt.InvalidModelStateResponseFactory = ctx =>
    {
        var errors = ctx.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                k => k.Key,
                v => v.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
            );

        var response = ApiResponse<object>.ValidationFail(errors);
        return new UnprocessableEntityObjectResult(response); // 422
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SaaSPlatform API", Version = "v1" });

    // 1. JWT Bearer Auth
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter ONLY your token in the text input below (Swagger will automatically add 'Bearer ').\r\n\r\nExample: \"eyJhbGci...\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // 2. Custom API Key Auth
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key Authentication. \r\n\r\n Enter your generated Plaintext API Key below.\r\n\r\nExample: \"sk_live_abc123.xyz456\"",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey"
    });

    // Apply JWT Bearer globally because almost all routes will eventually use it
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // Add Operation Filter for the API Key
    c.OperationFilter<ApiKeyAuthOperationFilter>();
});

// Database
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// DI — Auth services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IUsageService, UsageService>();

// JWT Authentication
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwt["Issuer"],
            ValidAudience            = jwt["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["Key"]!))
        };
        
        opt.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                // Prevent default ASP.NET Core behavior
                context.HandleResponse();

                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var response = ApiResponse<object>.Fail("Unauthorized. You must provide a valid Bearer token.");
                var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

                await context.Response.WriteAsync(json);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";

                var response = ApiResponse<object>.Fail("Forbidden. You do not have permission to access this resource.");
                var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

                await context.Response.WriteAsync(json);
            }
        };
    });

builder.Services.AddAuthorization(opt =>
{
    // Usage: [Authorize(Policy = "AdminOnly")] on any controller or action
    opt.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, "Admin"));
});

var app = builder.Build();

// ── Seed the database at startup (like php artisan db:seed) ──────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();     // apply any pending migrations
    await DatabaseSeeder.SeedAsync(db);   // seed data if tables are empty
}

// ── Middleware pipeline ──────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>(); // centralized exception handling

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Create an OperationFilter that only adds the ApiKey requirements to endpoints with [ApiKeyAuth]
public class ApiKeyAuthOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasApiKeyAuthAttribute = context.MethodInfo.DeclaringType!.GetCustomAttributes(true).OfType<ApiKeyAuthAttribute>().Any() ||
                                     context.MethodInfo.GetCustomAttributes(true).OfType<ApiKeyAuthAttribute>().Any();

        if (hasApiKeyAuthAttribute)
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
                    },
                    Array.Empty<string>()
                }
            });
        }
    }
}
