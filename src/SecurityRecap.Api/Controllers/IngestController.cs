using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Api.Services;
using SecurityRecap.Core.Exceptions;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/ingest")]
[Authorize(Policy = "OperationalUser")]
public class IngestController : BaseApiController
{
    private readonly IIngestionService _ingestionService;

    public IngestController(IIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    [HttpPost("report")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB
    public async Task<ActionResult<ApiResponse<IngestReportResponse>>> IngestReport(
        [FromForm] Guid propertyId, IFormFile file, [FromForm] string? externalId = null)
    {
        if (file is null)
            return BadRequest(ApiResponse<IngestReportResponse>.Fail("File is required"));

        if (file.Length == 0)
            return BadRequest(ApiResponse<IngestReportResponse>.Fail("File is empty"));

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<IngestReportResponse>.Fail("Only PDF files are accepted"));

        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();

        try
        {
            var outcome = await _ingestionService.IngestReportAsync(
                tenantId, userId, userRole, propertyId, file.OpenReadStream(), file.FileName, externalId);
            return Ok(ApiResponse<IngestReportResponse>.Ok(new IngestReportResponse(outcome.ReportId, outcome.AlreadyIngested)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<IngestReportResponse>.Fail(ex.Message));
        }
        catch (ClaudeApiException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<IngestReportResponse>.Fail(ClaudeFailureFormatter.ToUserMessage(ex)));
        }
    }

    [HttpPost("report")]
    [Consumes("application/json")]
    [RequestSizeLimit(30 * 1024 * 1024)] // 30 MB to accommodate base64 overhead on a ~20MB PDF
    public async Task<ActionResult<ApiResponse<IngestReportResponse>>> IngestReportJson([FromBody] IngestReportJsonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PdfBase64))
            return BadRequest(ApiResponse<IngestReportResponse>.Fail("pdfBase64 is required"));

        if (string.IsNullOrWhiteSpace(request.FileName)
            || !request.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<IngestReportResponse>.Fail("fileName must end with .pdf"));

        byte[] pdfBytes;
        try
        {
            pdfBytes = Convert.FromBase64String(request.PdfBase64);
        }
        catch (FormatException)
        {
            return BadRequest(ApiResponse<IngestReportResponse>.Fail("pdfBase64 is not valid base64"));
        }

        if (pdfBytes.Length == 0)
            return BadRequest(ApiResponse<IngestReportResponse>.Fail("Decoded PDF is empty"));

        if (!LooksLikePdf(pdfBytes))
            return BadRequest(ApiResponse<IngestReportResponse>.Fail("Decoded content does not look like a PDF"));

        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();

        try
        {
            await using var stream = new MemoryStream(pdfBytes);
            var outcome = await _ingestionService.IngestReportAsync(
                tenantId, userId, userRole, request.PropertyId, stream, request.FileName, request.ExternalId);
            return Ok(ApiResponse<IngestReportResponse>.Ok(new IngestReportResponse(outcome.ReportId, outcome.AlreadyIngested)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<IngestReportResponse>.Fail(ex.Message));
        }
        catch (ClaudeApiException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<IngestReportResponse>.Fail(ClaudeFailureFormatter.ToUserMessage(ex)));
        }
    }

    private static bool LooksLikePdf(byte[] bytes)
    {
        if (bytes.Length < 4) return false;
        return bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46; // %PDF
    }
}
