namespace SecurityRecap.Core.Interfaces;

using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

public record UserSummary(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt,
    IReadOnlyList<Guid> PropertyIds);

public record CreateUserResult(UserSummary User, string TempPassword);

public record ResetPasswordResult(string TempPassword);

public interface IUserManagementService
{
    Task<IReadOnlyList<UserSummary>> ListAsync(Guid tenantId);
    Task<UserSummary?> GetAsync(Guid tenantId, Guid userId);
    Task<CreateUserResult> CreateAsync(Guid tenantId, string email, string fullName, UserRole role, IEnumerable<Guid> propertyIds);
    Task<UserSummary> UpdateAsync(Guid tenantId, Guid userId, string fullName, UserRole role);
    Task<UserSummary> SetActiveAsync(Guid tenantId, Guid userId, bool isActive);
    Task<UserSummary> ReplacePropertiesAsync(Guid tenantId, Guid userId, IEnumerable<Guid> propertyIds);
    Task<ResetPasswordResult> ResetPasswordAsync(Guid tenantId, Guid userId);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}
