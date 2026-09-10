using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/recap")]
[Authorize]
public class RecapController : BaseApiController
{
    /// <summary>
    /// Generating a recap runs a model call over a whole period, so the range is bounded to
    /// keep one click from turning into a year of analysis.
    /// </summary>
    private const int MaxDays = 190;

    private readonly IRecapService _recap;

    public RecapController(IRecapService recap)
    {
        _recap = recap;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<RecapResult>>> Get(
        [FromQuery] Guid propertyId,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] bool includeNarrative = true)
    {
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-29);

        if (start > end)
            return BadRequest(ApiResponse<RecapResult>.Fail("The start date must fall on or before the end date."));

        var days = end.DayNumber - start.DayNumber + 1;
        if (days > MaxDays)
            return BadRequest(ApiResponse<RecapResult>.Fail(
                $"That range covers {days} days. Choose {MaxDays} days or fewer."));

        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();

        try
        {
            if (!includeNarrative)
            {
                var data = await _recap.BuildDataAsync(
                    tenantId, userId, userRole, propertyId, start, end, HttpContext.RequestAborted);
                return Ok(ApiResponse<RecapResult>.Ok(new RecapResult(data, null, null)));
            }

            var result = await _recap.BuildAsync(
                tenantId, userId, userRole, propertyId, start, end, HttpContext.RequestAborted);
            return Ok(ApiResponse<RecapResult>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<RecapResult>.Fail(ex.Message));
        }
    }
}
