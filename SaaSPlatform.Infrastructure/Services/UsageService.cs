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
            .Join(_db.SubscriptionPlans, sub => sub.PlanId, plan => plan.Id, (sub, plan) => new { sub, plan })
            .FirstOrDefaultAsync(ct);

        if (activeSub == null)
            throw new InvalidOperationException("No active subscription found.");

        int monthlyLimit = activeSub.plan.ApiCallsPerMonth;

        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // 2. Atomic Increment Query using SQL Server Raw
        var rowsAffected = await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE OrganizationUsageMonthly 
            SET ApiCallCount = ApiCallCount + 1 
            WHERE OrganizationId = {0} 
              AND YearMonth = {1} 
              AND ApiCallCount < {2}", 
            organizationId, yearMonth, monthlyLimit);

        // 3. Handle First-Time Row Creation or Rate Limit Hit
        if (rowsAffected == 0)
        {
            // Let's check if the row exists
            var existing = await _db.OrganizationUsageMonthly.FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.YearMonth == yearMonth, ct);

            if (existing != null)
            {
                // Row exists but no rows affected by UPDATE means: ApiCallCount >= monthlyLimit
                throw new RateLimitExceededException("Monthly API call limit exceeded.");
            }
            else
            {
                // Row does not exist for this month yet. Insert the initial record (1 used)
                var initialRecord = new OrganizationUsageMonthly
                {
                    OrganizationId = organizationId,
                    YearMonth = yearMonth,
                    ApiCallCount = 1
                };
                _db.OrganizationUsageMonthly.Add(initialRecord);
                await _db.SaveChangesAsync(ct);
            }
        }

        // 4. Append the verbose usage log
        var usageRecord = new UsageRecord
        {
            OrganizationId = organizationId,
            ApiKeyId = apiKeyId,
            Endpoint = request.Endpoint,
            StatusCode = request.StatusCode,
            OccurredAt = now
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
}
