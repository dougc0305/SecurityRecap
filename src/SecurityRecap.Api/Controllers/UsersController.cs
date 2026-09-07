using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = "AdminOnly")]
public class UsersController : BaseApiController
{
    private readonly IUserManagementService _users;

    public UsersController(IUserManagementService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserDto>>>> List()
    {
        var tenantId = GetTenantId();
        var summaries = await _users.ListAsync(tenantId);
        return Ok(ApiResponse<IReadOnlyList<UserDto>>.Ok(summaries.Select(ToDto).ToList()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Get(Guid id)
    {
        var tenantId = GetTenantId();
        var summary = await _users.GetAsync(tenantId, id);
        return summary is null
            ? NotFound(ApiResponse<UserDto>.Fail("User not found"))
            : Ok(ApiResponse<UserDto>.Ok(ToDto(summary)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CreateUserResponse>>> Create([FromBody] CreateUserRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return BadRequest(ApiResponse<CreateUserResponse>.Fail($"Unknown role '{request.Role}'"));

        try
        {
            var tenantId = GetTenantId();
            var result = await _users.CreateAsync(tenantId, request.Email, request.FullName, role, request.PropertyIds ?? Array.Empty<Guid>());
            return Ok(ApiResponse<CreateUserResponse>.Ok(new CreateUserResponse(ToDto(result.User), result.TempPassword)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CreateUserResponse>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return BadRequest(ApiResponse<UserDto>.Fail($"Unknown role '{request.Role}'"));

        var tenantId = GetTenantId();
        try
        {
            var summary = await _users.UpdateAsync(tenantId, id, request.FullName, role);
            return Ok(ApiResponse<UserDto>.Ok(ToDto(summary)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<UserDto>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:guid}/active")]
    public async Task<ActionResult<ApiResponse<UserDto>>> SetActive(Guid id, [FromBody] SetActiveRequest request)
    {
        var tenantId = GetTenantId();
        var callerId = GetUserId();
        if (callerId == id && !request.IsActive)
            return BadRequest(ApiResponse<UserDto>.Fail("You cannot deactivate your own account."));

        try
        {
            var summary = await _users.SetActiveAsync(tenantId, id, request.IsActive);
            return Ok(ApiResponse<UserDto>.Ok(ToDto(summary)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<UserDto>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:guid}/properties")]
    public async Task<ActionResult<ApiResponse<UserDto>>> ReplaceProperties(Guid id, [FromBody] AssignPropertiesRequest request)
    {
        var tenantId = GetTenantId();
        try
        {
            var propertyIds = request.PropertyIds ?? Array.Empty<Guid>();
            var summaryIds = request.SummaryPropertyIds ?? Array.Empty<Guid>();
            var alertIds = request.AlertPropertyIds ?? Array.Empty<Guid>();

            // Refuse to mail someone about a property they cannot open. Without this the
            // summary and alert flags would quietly become a second, invisible access path.
            var orphaned = summaryIds.Concat(alertIds).Except(propertyIds).ToList();
            if (orphaned.Count > 0)
                return BadRequest(ApiResponse<UserDto>.Fail(
                    "A user can only receive summaries or alerts for properties they are assigned to."));

            var assignments = propertyIds
                .Distinct()
                .Select(pid => new PropertyAssignment(pid, summaryIds.Contains(pid), alertIds.Contains(pid)))
                .ToList();

            var summary = await _users.ReplacePropertiesAsync(tenantId, id, assignments);
            return Ok(ApiResponse<UserDto>.Ok(ToDto(summary)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<UserDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
        }
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ApiResponse<ResetPasswordResponse>>> ResetPassword(Guid id)
    {
        var tenantId = GetTenantId();
        try
        {
            var result = await _users.ResetPasswordAsync(tenantId, id);
            return Ok(ApiResponse<ResetPasswordResponse>.Ok(new ResetPasswordResponse(result.TempPassword)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ResetPasswordResponse>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ResetPasswordResponse>.Fail(ex.Message));
        }
    }

    private static UserDto ToDto(UserSummary s) => new(
        s.Id, s.Email, s.FullName, s.Role.ToString(), s.IsActive, s.MustChangePassword, s.CreatedAt,
        s.PropertyIds, s.SummaryPropertyIds, s.AlertPropertyIds);
}
