namespace SecurityRecap.Core.Interfaces;

using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

/// <summary>A property a user is assigned to, and whether they are mailed its summary.</summary>
public record PropertyAssignment(Guid PropertyId, bool ReceivesSummary, bool ReceivesAlerts);

public record UserSummary(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt,
    IReadOnlyList<Guid> PropertyIds,
    IReadOnlyList<Guid> SummaryPropertyIds,
    IReadOnlyList<Guid> AlertPropertyIds);

public record CreateUserResult(UserSummary User, string TempPassword);

public record ResetPasswordResult(string TempPassword);

public interface IUserManagementService
{
    Task<IReadOnlyList<UserSummary>> ListAsync(Guid tenantId);
    Task<UserSummary?> GetAsync(Guid tenantId, Guid userId);
    Task<CreateUserResult> CreateAsync(Guid tenantId, string email, string fullName, UserRole role, IEnumerable<Guid> propertyIds);
    Task<UserSummary> UpdateAsync(Guid tenantId, Guid userId, string fullName, UserRole role);
    Task<UserSummary> SetActiveAsync(Guid tenantId, Guid userId, bool isActive);
    /// <summary>
    /// Replaces the user's property assignments wholesale, including the per-property
    /// summary flag. Taking both together avoids a property-only update silently clearing
    /// who receives the summary.
    /// </summary>
    Task<UserSummary> ReplacePropertiesAsync(Guid tenantId, Guid userId, IEnumerable<PropertyAssignment> assignments);
    Task<ResetPasswordResult> ResetPasswordAsync(Guid tenantId, Guid userId);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}
