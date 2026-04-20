using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/incidents")]
[Authorize]
public class IncidentsController : BaseApiController
{
    private readonly IIncidentService _incidentService;

    public IncidentsController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<Incident>>> GetAll(
        [FromQuery] Guid? propertyId, [FromQuery] IncidentType? type,
        [FromQuery] Severity? severity, [FromQuery] DateTime? from,
        [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();
        var (items, totalCount) = await _incidentService.GetAllAsync(tenantId, userId, userRole, propertyId, type, severity, from, to, page, pageSize);
        return Ok(new PagedResponse<Incident> { Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount });
    }
}
