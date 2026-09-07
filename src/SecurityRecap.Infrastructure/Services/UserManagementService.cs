using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Infrastructure.Services;

public class UserManagementService : IUserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public UserManagementService(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IReadOnlyList<UserSummary>> ListAsync(Guid tenantId)
    {
        var users = await _db.Users
            .Where(u => u.TenantId == tenantId)
            .Include(u => u.UserProperties)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return users.Select(ToSummary).ToList();
    }

    public async Task<UserSummary?> GetAsync(Guid tenantId, Guid userId)
    {
        var user = await _db.Users
            .Where(u => u.TenantId == tenantId && u.Id == userId)
            .Include(u => u.UserProperties)
            .FirstOrDefaultAsync();
        return user is null ? null : ToSummary(user);
    }

    public async Task<CreateUserResult> CreateAsync(Guid tenantId, string email, string fullName, UserRole role, IEnumerable<Guid> propertyIds)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            throw new InvalidOperationException("A user with that email already exists.");

        var validPropertyIds = await ValidatePropertyIdsAsync(tenantId, propertyIds);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            TenantId = tenantId,
            Role = role,
            IsActive = true,
            EmailConfirmed = true,
            MustChangePassword = true
        };

        var tempPassword = GenerateTempPassword();
        var result = await _userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        foreach (var pid in validPropertyIds)
            _db.UserProperties.Add(new UserProperty { UserId = user.Id, PropertyId = pid });
        await _db.SaveChangesAsync();

        var loaded = await _db.Users.Include(u => u.UserProperties).FirstAsync(u => u.Id == user.Id);
        return new CreateUserResult(ToSummary(loaded), tempPassword);
    }

    public async Task<UserSummary> UpdateAsync(Guid tenantId, Guid userId, string fullName, UserRole role)
    {
        var user = await _db.Users
            .Include(u => u.UserProperties)
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId)
            ?? throw new KeyNotFoundException("User not found");

        user.FullName = fullName;
        user.Role = role;
        await _db.SaveChangesAsync();
        return ToSummary(user);
    }

    public async Task<UserSummary> SetActiveAsync(Guid tenantId, Guid userId, bool isActive)
    {
        var user = await _db.Users
            .Include(u => u.UserProperties)
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId)
            ?? throw new KeyNotFoundException("User not found");

        user.IsActive = isActive;
        await _db.SaveChangesAsync();
        return ToSummary(user);
    }

    public async Task<UserSummary> ReplacePropertiesAsync(
        Guid tenantId, Guid userId, IEnumerable<PropertyAssignment> assignments)
    {
        var user = await _db.Users
            .Include(u => u.UserProperties)
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId)
            ?? throw new KeyNotFoundException("User not found");

        // Last write wins if the same property appears twice.
        var requested = assignments
            .GroupBy(a => a.PropertyId)
            .ToDictionary(g => g.Key, g => g.Last());

        var validIds = await ValidatePropertyIdsAsync(tenantId, requested.Keys);

        _db.UserProperties.RemoveRange(user.UserProperties);
        foreach (var pid in validIds)
        {
            _db.UserProperties.Add(new UserProperty
            {
                UserId = user.Id,
                PropertyId = pid,
                ReceivesSummary = requested[pid].ReceivesSummary,
                ReceivesAlerts = requested[pid].ReceivesAlerts
            });
        }
        await _db.SaveChangesAsync();

        var reloaded = await _db.Users.Include(u => u.UserProperties).FirstAsync(u => u.Id == user.Id);
        return ToSummary(reloaded);
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(Guid tenantId, Guid userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId)
            ?? throw new KeyNotFoundException("User not found");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var tempPassword = GenerateTempPassword();
        var result = await _userManager.ResetPasswordAsync(user, token, tempPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        user.MustChangePassword = true;
        await _db.SaveChangesAsync();
        return new ResetPasswordResult(tempPassword);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return false;

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            return false;

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);
        return true;
    }

    private async Task<List<Guid>> ValidatePropertyIdsAsync(Guid tenantId, IEnumerable<Guid> propertyIds)
    {
        var requested = propertyIds.Distinct().ToList();
        if (requested.Count == 0) return new List<Guid>();

        var owned = await _db.Properties
            .Where(p => p.TenantId == tenantId && requested.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();

        var rejected = requested.Except(owned).ToList();
        if (rejected.Count > 0)
            throw new InvalidOperationException($"Property ids not in tenant: {string.Join(", ", rejected)}");

        return owned;
    }

    private static UserSummary ToSummary(ApplicationUser u) => new(
        u.Id, u.Email!, u.FullName, u.Role, u.IsActive, u.MustChangePassword, u.CreatedAt,
        u.UserProperties.Select(up => up.PropertyId).ToList(),
        u.UserProperties.Where(up => up.ReceivesSummary).Select(up => up.PropertyId).ToList(),
        u.UserProperties.Where(up => up.ReceivesAlerts).Select(up => up.PropertyId).ToList());

    private static string GenerateTempPassword()
    {
        // 16 chars: at least one upper, lower, digit, non-alphanumeric to satisfy default Identity policy
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digit = "23456789";
        const string special = "!@#$%^&*";
        const string all = upper + lower + digit + special;

        Span<char> buf = stackalloc char[16];
        buf[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        buf[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        buf[2] = digit[RandomNumberGenerator.GetInt32(digit.Length)];
        buf[3] = special[RandomNumberGenerator.GetInt32(special.Length)];
        for (var i = 4; i < buf.Length; i++)
            buf[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        // Fisher-Yates shuffle
        for (var i = buf.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (buf[i], buf[j]) = (buf[j], buf[i]);
        }
        return new string(buf);
    }
}
