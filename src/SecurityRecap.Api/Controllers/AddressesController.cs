using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/addresses")]
[Authorize]
public class AddressesController : BaseApiController
{
    private readonly IAddressOfInterestService _service;

    public AddressesController(IAddressOfInterestService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<AddressOfInterest>>> GetAll(
        [FromQuery] Guid? propertyId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();
        var (items, totalCount) = await _service.GetAllAsync(tenantId, userId, userRole, propertyId, page, pageSize);
        return Ok(new PagedResponse<AddressOfInterest> { Data = items, Page = page, PageSize = pageSize, TotalCount = totalCount });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> GetById(Guid id)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();
        var (address, incidents) = await _service.GetByIdAsync(tenantId, userId, userRole, id);
        if (address is null)
            return NotFound(ApiResponse<object>.Fail("Address not found"));
        return Ok(ApiResponse<object>.Ok(new { address, incidents }));
    }
}
