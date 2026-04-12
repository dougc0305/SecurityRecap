using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/ingest")]
[Authorize]
public class IngestController : BaseApiController
{
    private readonly IIngestionService _ingestionService;

    public IngestController(IIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    [HttpPost("report")]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB
    public async Task<ActionResult<ApiResponse<object>>> IngestReport(
        [FromForm] Guid propertyId, IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("File is empty"));

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Fail("Only PDF files are accepted"));

        var tenantId = GetTenantId();

        try
        {
            var reportId = await _ingestionService.IngestReportAsync(
                tenantId, propertyId, file.OpenReadStream(), file.FileName);
            return Ok(ApiResponse<object>.Ok(new { reportId }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
