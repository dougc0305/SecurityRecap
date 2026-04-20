using System.ComponentModel.DataAnnotations;

namespace SecurityRecap.Api.DTOs;

public record ApiKeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? RevokedAt);

public record CreateApiKeyRequest(
    [Required, MaxLength(100)] string Name);

public record CreateApiKeyResponse(ApiKeyDto ApiKey, string RawKey);
