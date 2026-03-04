namespace SaaSPlatform.Application.DTOs;

public class GetUsageAnalyticsRequest
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public class DailyUsageBreakdown
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class UsageAnalyticsResponse
{
    public int TotalUsage { get; set; }
    public int Limit { get; set; }
    public int RemainingQuota { get; set; }
    public List<DailyUsageBreakdown> Breakdown { get; set; } = new();
}
