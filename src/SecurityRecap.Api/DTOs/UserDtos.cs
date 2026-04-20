using System.ComponentModel.DataAnnotations;

namespace SecurityRecap.Api.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt,
    IReadOnlyList<Guid> PropertyIds);

public record CreateUserRequest(
    [Required, EmailAddress] string Email,
    [Required] string FullName,
    [Required] string Role,
    IReadOnlyList<Guid>? PropertyIds);

public record CreateUserResponse(UserDto User, string TempPassword);

public record UpdateUserRequest(
    [Required] string FullName,
    [Required] string Role);

public record SetActiveRequest(bool IsActive);

public record AssignPropertiesRequest(IReadOnlyList<Guid> PropertyIds);

public record ResetPasswordResponse(string TempPassword);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);
