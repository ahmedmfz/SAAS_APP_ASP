using SaaSPlatform.Application.DTOs;

namespace SaaSPlatform.Application.Interfaces;

public interface IUsageService
{
    /// <summary>
    /// Atomically increments usage and inserts a log, guarding against race-conditions.
    /// Throws DomainExceptions (or returns false/failure) if limits are exceeded.
    /// </summary>
    Task<bool> RecordUsageAsync(Guid organizationId, Guid apiKeyId, RecordUsageRequest request, CancellationToken ct);
    Task<UsageAnalyticsResponse> GetUsageAnalyticsAsync(Guid organizationId, DateTime from, DateTime to, CancellationToken ct);
    Task<List<UsageRecordResponse>> GetUsageRecordsAsync(Guid organizationId, Guid? userId, Domain.Enums.UserRole role, CancellationToken ct);
}
