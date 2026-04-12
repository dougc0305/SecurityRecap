using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

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
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null ? Guid.Parse(claim) : throw new UnauthorizedAccessException("Missing sub claim");
    }
}
