using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SaaSPlatform.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // EF may run from solution root OR from Api folder depending on command.
        var cwd = Directory.GetCurrentDirectory();

        // Try 2 candidates:
        // 1) current directory
        // 2) ..\SaaSPlatform.Api  (if we are in solution root)
        var candidates = new[]
        {
            cwd,
            Path.Combine(cwd, "SaaSPlatform.Api"),
            Path.GetFullPath(Path.Combine(cwd, "..", "SaaSPlatform.Api")),
        };

        string? basePath = candidates.FirstOrDefault(p => File.Exists(Path.Combine(p, "appsettings.json")));

        if (basePath is null)
            throw new FileNotFoundException("Could not find appsettings.json. Tried: " + string.Join(" | ", candidates));

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionStrings:Default is missing in appsettings.json");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cs)
            .Options;

        return new AppDbContext(options);
    }
}