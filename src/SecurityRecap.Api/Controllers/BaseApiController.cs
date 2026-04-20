using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SecurityRecap.Core.Enums;

namespace SecurityRecap.Api.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected Guid GetTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return claim is not null ? Guid.Parse(claim) : throw new UnauthorizedAccessException("Missing tenant_id claim");
    }

    protected Guid GetUserId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null ? Guid.Parse(claim) : throw new UnauthorizedAccessException("Missing user id claim");
    }

    protected UserRole GetUserRole()
    {
        var claim = User.FindFirst("role")?.Value
            ?? User.FindFirst(ClaimTypes.Role)?.Value;

        if (claim is not null && Enum.TryParse<UserRole>(claim, ignoreCase: true, out var role))
            return role;

        throw new UnauthorizedAccessException("Missing role claim");
    }
}
