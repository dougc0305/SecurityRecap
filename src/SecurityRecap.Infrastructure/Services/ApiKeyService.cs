using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Infrastructure.Services;

public class ApiKeyService : IApiKeyService
{
    public const string KeyPrefix = "sr_live_";

    private readonly AppDbContext _db;

    public ApiKeyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ApiKeySummary>> ListAsync(Guid tenantId)
    {
        var rows = await _db.ApiKeys
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return rows.Select(ToSummary).ToList();
    }

    public async Task<ApiKeyCreated> CreateAsync(Guid tenantId, Guid createdByUserId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var raw = GenerateRawKey();
        var hash = Hash(raw);

        var key = new ApiKey
        {
            TenantId = tenantId,
            Name = name.Trim(),
            KeyHash = hash,
            KeyPrefix = raw[..12],
            CreatedByUserId = createdByUserId
        };
        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync();

        return new ApiKeyCreated(ToSummary(key), raw);
    }

    public async Task<bool> RevokeAsync(Guid tenantId, Guid apiKeyId)
    {
        var key = await _db.ApiKeys.FirstOrDefaultAsync(a => a.Id == apiKeyId && a.TenantId == tenantId);
        if (key is null) return false;
        if (key.RevokedAt is null)
        {
            key.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return true;
    }

    public async Task<ApiKeyAuthResult?> ValidateAndTouchAsync(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey) || !rawKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
            return null;

        var hash = Hash(rawKey);
        var key = await _db.ApiKeys.FirstOrDefaultAsync(a => a.KeyHash == hash);
        if (key is null || key.RevokedAt is not null) return null;

        key.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new ApiKeyAuthResult(key.Id, key.TenantId, key.Name);
    }

    private static string GenerateRawKey()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        // URL-safe base64, strip padding; yields 32 chars from 24 bytes
        var body = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return KeyPrefix + body;
    }

    private static string Hash(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    private static ApiKeySummary ToSummary(ApiKey a) =>
        new(a.Id, a.Name, a.KeyPrefix, a.CreatedAt, a.LastUsedAt, a.RevokedAt);
}
