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
    IReadOnlyList<Guid> PropertyIds,
    IReadOnlyList<Guid> SummaryPropertyIds,
    IReadOnlyList<Guid> AlertPropertyIds);

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

/// <summary>
/// Assignments are replaced wholesale. SummaryPropertyIds must be a subset of PropertyIds:
/// a user cannot be mailed reports about a property they are not assigned to.
/// </summary>
public record AssignPropertiesRequest(
    IReadOnlyList<Guid> PropertyIds,
    IReadOnlyList<Guid>? SummaryPropertyIds,
    IReadOnlyList<Guid>? AlertPropertyIds);

public record ResetPasswordResponse(string TempPassword);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);
