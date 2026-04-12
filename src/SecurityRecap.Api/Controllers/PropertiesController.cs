using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/properties")]
[Authorize]
public class PropertiesController : BaseApiController
{
    private readonly IPropertyService _propertyService;

    public PropertiesController(IPropertyService propertyService)
    {
        _propertyService = propertyService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<PropertyResponse>>>> GetAll()
    {
        var tenantId = GetTenantId();
        var properties = await _propertyService.GetAllAsync(tenantId);
        var response = properties.Select(ToResponse);
        return Ok(ApiResponse<IEnumerable<PropertyResponse>>.Ok(response));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PropertyResponse>>> GetById(Guid id)
    {
        var tenantId = GetTenantId();
        var property = await _propertyService.GetByIdAsync(tenantId, id);
        if (property is null) return NotFound(ApiResponse<PropertyResponse>.Fail("Property not found"));
        return Ok(ApiResponse<PropertyResponse>.Ok(ToResponse(property)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PropertyResponse>>> Create([FromBody] CreatePropertyRequest request)
    {
        var tenantId = GetTenantId();
        var property = new Property
        {
            Name = request.Name,
            Address = request.Address,
            City = request.City,
            State = request.State,
            Zip = request.Zip,
            SecurityCompany = request.SecurityCompany,
            ReportEmail = request.ReportEmail,
            Timezone = request.Timezone
        };

        var created = await _propertyService.CreateAsync(tenantId, property);
        return CreatedAtAction(nameof(GetById), new { id = created.Id },
            ApiResponse<PropertyResponse>.Ok(ToResponse(created)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PropertyResponse>>> Update(Guid id, [FromBody] UpdatePropertyRequest request)
    {
        var tenantId = GetTenantId();
        var property = new Property
        {
            Name = request.Name,
            Address = request.Address,
            City = request.City,
            State = request.State,
            Zip = request.Zip,
            SecurityCompany = request.SecurityCompany,
            ReportEmail = request.ReportEmail,
            Timezone = request.Timezone,
            IsActive = request.IsActive
        };

        try
        {
            var updated = await _propertyService.UpdateAsync(tenantId, id, property);
            return Ok(ApiResponse<PropertyResponse>.Ok(ToResponse(updated)));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<PropertyResponse>.Fail("Property not found"));
        }
    }

    private static PropertyResponse ToResponse(Property p) => new(
        p.Id, p.Name, p.Address, p.City, p.State, p.Zip,
        p.SecurityCompany, p.ReportEmail, p.Timezone, p.IsActive, p.CreatedAt
    );
}
