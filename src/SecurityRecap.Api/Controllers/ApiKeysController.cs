using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/api-keys")]
[Authorize(Policy = "AdminOnly")]
public class ApiKeysController : BaseApiController
{
    private readonly IApiKeyService _apiKeys;

    public ApiKeysController(IApiKeyService apiKeys)
    {
        _apiKeys = apiKeys;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ApiKeyDto>>>> List()
    {
        var tenantId = GetTenantId();
        var summaries = await _apiKeys.ListAsync(tenantId);
        return Ok(ApiResponse<IReadOnlyList<ApiKeyDto>>.Ok(summaries.Select(ToDto).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CreateApiKeyResponse>>> Create([FromBody] CreateApiKeyRequest request)
    {
        var tenantId = GetTenantId();
        var createdBy = GetUserId();
        try
        {
            var result = await _apiKeys.CreateAsync(tenantId, createdBy, request.Name);
            return Ok(ApiResponse<CreateApiKeyResponse>.Ok(new CreateApiKeyResponse(ToDto(result.Summary), result.RawKey)));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<CreateApiKeyResponse>.Fail(ex.Message));
        }
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<ActionResult<ApiResponse<object>>> Revoke(Guid id)
    {
        var tenantId = GetTenantId();
        var ok = await _apiKeys.RevokeAsync(tenantId, id);
        return ok
            ? Ok(ApiResponse<object>.Ok(new { revoked = true }))
            : NotFound(ApiResponse<object>.Fail("API key not found"));
    }

    private static ApiKeyDto ToDto(ApiKeySummary s) =>
        new(s.Id, s.Name, s.KeyPrefix, s.CreatedAt, s.LastUsedAt, s.RevokedAt);
}
