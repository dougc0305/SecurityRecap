using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/vehicles")]
[Authorize]
public class VehiclesController : BaseApiController
{
    private readonly IVehicleService _vehicleService;

    public VehiclesController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<Vehicle>>> GetAll(
        [FromQuery] Guid? propertyId, [FromQuery] string? plate,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();
        var (items, totalCount) = await _vehicleService.GetAllAsync(tenantId, userId, userRole, propertyId, plate, page, pageSize);
        return Ok(new PagedResponse<Vehicle> { Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Vehicle>>> GetById(Guid id)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();
        var vehicle = await _vehicleService.GetByIdAsync(tenantId, userId, userRole, id);
        if (vehicle is null) return NotFound(ApiResponse<Vehicle>.Fail("Vehicle not found"));
        return Ok(ApiResponse<Vehicle>.Ok(vehicle));
    }
}
