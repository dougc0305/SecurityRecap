namespace SecurityRecap.Core.Interfaces;

public record ApiKeySummary(
    Guid Id,
    string Name,
    string KeyPrefix,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? RevokedAt);

public record ApiKeyCreated(ApiKeySummary Summary, string RawKey);

public record ApiKeyAuthResult(Guid ApiKeyId, Guid TenantId, string Name);

public interface IApiKeyService
{
    Task<IReadOnlyList<ApiKeySummary>> ListAsync(Guid tenantId);
    Task<ApiKeyCreated> CreateAsync(Guid tenantId, Guid createdByUserId, string name);
    Task<bool> RevokeAsync(Guid tenantId, Guid apiKeyId);
    Task<ApiKeyAuthResult?> ValidateAndTouchAsync(string rawKey);
}
