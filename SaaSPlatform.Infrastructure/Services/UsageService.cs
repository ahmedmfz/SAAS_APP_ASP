using Microsoft.EntityFrameworkCore;
using SaaSPlatform.Application.DTOs;
using SaaSPlatform.Application.Exceptions;
using SaaSPlatform.Application.Interfaces;
using SaaSPlatform.Domain.Entities;
using SaaSPlatform.Infrastructure.Persistence;

namespace SaaSPlatform.Infrastructure.Services;

public class UsageService : IUsageService
{
    private readonly AppDbContext _db;

    public UsageService(AppDbContext db) => _db = db;

    public async Task<bool> RecordUsageAsync(Guid organizationId, Guid apiKeyId, RecordUsageRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var yearMonth = int.Parse(now.ToString("yyyyMM")); // 202603

        // 1. Validate Active Subscription and Limits
        var activeSub = await _db.OrganizationSubscriptions
            .Where(x => x.OrganizationId == organizationId && x.StartAt <= now && x.EndAt >= now)
            .FirstOrDefaultAsync(ct);

        if (activeSub == null)
            throw new InvalidOperationException("No active subscription found.");

        int orgMonthlyLimit  = activeSub.ApiCallsMonthly;
        int userMonthlyLimit = activeSub.ApiCallsPerUser;

        // Resolve which user owns this API key (nullable)
        var apiKeyUserId = await _db.ApiKeys
            .Where(k => k.Id == apiKeyId)
            .Select(k => k.UserId)
            .FirstOrDefaultAsync(ct);

        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // 2. Enforce ORG-WIDE monthly limit (atomic SQL increment)
        var orgRowsAffected = await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE OrganizationUsageMonthly 
            SET ApiCallCount = ApiCallCount + 1 
            WHERE OrganizationId = {0} 
              AND YearMonth = {1} 
              AND ApiCallCount < {2}",
            organizationId, yearMonth, orgMonthlyLimit);

        if (orgRowsAffected == 0)
        {
            var existing = await _db.OrganizationUsageMonthly.FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.YearMonth == yearMonth, ct);

            if (existing != null)
                throw new RateLimitExceededException("Organization monthly API call limit exceeded.");

            // First usage this month for the org
            _db.OrganizationUsageMonthly.Add(new Domain.Entities.OrganizationUsageMonthly
            {
                OrganizationId = organizationId,
                YearMonth      = yearMonth,
                ApiCallCount   = 1
            });
            await _db.SaveChangesAsync(ct);
        }

        // 3. Enforce PER-USER monthly limit (only if key is linked to a user)
        if (apiKeyUserId.HasValue)
        {
            var userId = apiKeyUserId.Value;

            var userRowsAffected = await _db.Database.ExecuteSqlRawAsync(@"
                UPDATE UserUsageMonthly 
                SET ApiCallCount = ApiCallCount + 1 
                WHERE UserId = {0} 
                  AND YearMonth = {1} 
                  AND ApiCallCount < {2}",
                userId, yearMonth, userMonthlyLimit);

            if (userRowsAffected == 0)
            {
                var existingUser = await _db.UserUsageMonthly.FirstOrDefaultAsync(
                    x => x.UserId == userId && x.YearMonth == yearMonth, ct);

                if (existingUser != null)
                    throw new RateLimitExceededException("User monthly API call limit exceeded.");

                // First usage this month for this user
                _db.UserUsageMonthly.Add(new Domain.Entities.UserUsageMonthly
                {
                    UserId         = userId,
                    OrganizationId = organizationId,
                    YearMonth      = yearMonth,
                    ApiCallCount   = 1
                });
                await _db.SaveChangesAsync(ct);
            }
        }

        // 4. Append the verbose usage log
        var usageRecord = new UsageRecord
        {
            OrganizationId = organizationId,
            ApiKeyId       = apiKeyId,
            Endpoint       = request.Endpoint,
            StatusCode     = request.StatusCode,
            OccurredAt     = now
        };

        _db.UsageRecords.Add(usageRecord);

        // Update ApiKey LastUsedAt
        var apiKey = new ApiKey { Id = apiKeyId };
        _db.ApiKeys.Attach(apiKey);
        apiKey.LastUsedAt = now;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<UsageAnalyticsResponse> GetUsageAnalyticsAsync(Guid organizationId, DateTime from, DateTime to, CancellationToken ct)
    {
        // 1. Get current tracking month and active plan limit to determine quota
        var now = DateTime.UtcNow;
        var yearMonth = int.Parse(now.ToString("yyyyMM"));
        
        var currentMonthlyUsage = await _db.OrganizationUsageMonthly
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.YearMonth == yearMonth, ct);

        // Fetch user plan limit to figure out remaining quota
        var activeSub = await _db.OrganizationSubscriptions
            .Where(x => x.OrganizationId == organizationId && x.StartAt <= now && x.EndAt >= now)
            .Join(_db.SubscriptionPlans, sub => sub.PlanId, plan => plan.Id, (sub, plan) => new { plan.ApiCallsPerMonth })
            .FirstOrDefaultAsync(ct);

        int planLimit = activeSub?.ApiCallsPerMonth ?? 0;
        int usedInMonth = (int)(currentMonthlyUsage?.ApiCallCount ?? 0);
        int remainingQuota = Math.Max(0, planLimit - usedInMonth);

        // 2. Aggregate the exact historical daily usage between the requested dates
        // The dates from API are likely Date components, so we ensure the End Date includes the entire day
        var inclusiveTo = to.Date.AddDays(1).AddTicks(-1);

        var breakdown = await _db.UsageRecords
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.OccurredAt >= from.Date && x.OccurredAt <= inclusiveTo)
            .GroupBy(x => x.OccurredAt.Date)
            .Select(g => new DailyUsageBreakdown
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        int totalUsageInPeriod = (int)breakdown.Sum(x => x.Count);

        return new UsageAnalyticsResponse
        {
            TotalUsage = totalUsageInPeriod,
            Limit = planLimit,
            RemainingQuota = remainingQuota,
            Breakdown = breakdown
        };
    }

    public async Task<List<UsageRecordResponse>> GetUsageRecordsAsync(Guid organizationId, Guid? userId, Domain.Enums.UserRole role, CancellationToken ct)
    {
        var query = _db.UsageRecords
            .AsNoTracking()
            .Include(x => x.ApiKey)
            .Where(x => x.OrganizationId == organizationId);

       

        return await query
            .OrderByDescending(x => x.OccurredAt)
            .Take(100) // Return latest 100 for safety, add pagination later if needed
            .Select(x => new UsageRecordResponse
            {
                Id = x.Id,
                ApiKeyId = x.ApiKeyId,
                Endpoint = x.Endpoint,
                StatusCode = x.StatusCode,
                OccurredAt = x.OccurredAt
            })
            .ToListAsync(ct);
    }
}
