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

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<Report>>> GetAll(
        [FromQuery] Guid? propertyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var tenantId = GetTenantId();
        var (items, totalCount) = await _reportService.GetAllAsync(tenantId, propertyId, page, pageSize);
        return Ok(new PagedResponse<Report> { Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Report>>> GetById(Guid id)
    {
        var tenantId = GetTenantId();
        var report = await _reportService.GetByIdAsync(tenantId, id);
        if (report is null) return NotFound(ApiResponse<Report>.Fail("Report not found"));
        return Ok(ApiResponse<Report>.Ok(report));
    }
}
