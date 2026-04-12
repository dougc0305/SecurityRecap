using System.ComponentModel.DataAnnotations;

namespace SecurityRecap.Api.DTOs;

public record LoginRequest(
    [Required] string Email,
    [Required] string Password
);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserInfo User
);

public record RefreshRequest(
    [Required] string RefreshToken
);

public record UserInfo(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    Guid TenantId
);
