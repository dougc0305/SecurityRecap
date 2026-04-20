using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController : BaseApiController
{
    private readonly IReportService _reportService;
    private readonly IBlobStorageService _blobStorage;

    public ReportsController(IReportService reportService, IBlobStorageService blobStorage)
    {
        _reportService = reportService;
        _blobStorage = blobStorage;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<Report>>> GetAll(
        [FromQuery] Guid? propertyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();
        var (items, totalCount) = await _reportService.GetAllAsync(tenantId, userId, userRole, propertyId, page, pageSize);
        return Ok(new PagedResponse<Report> { Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Report>>> GetById(Guid id)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();
        var report = await _reportService.GetByIdAsync(tenantId, userId, userRole, id);
        if (report is null) return NotFound(ApiResponse<Report>.Fail("Report not found"));
        return Ok(ApiResponse<Report>.Ok(report));
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> GetPdf(Guid id)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();
        var report = await _reportService.GetByIdAsync(tenantId, userId, userRole, id);
        if (report is null || string.IsNullOrWhiteSpace(report.RawPdfUrl))
            return NotFound();

        var stream = await _blobStorage.DownloadAsync(report.RawPdfUrl);
        return File(stream, "application/pdf", $"report-{id}.pdf");
    }
}
