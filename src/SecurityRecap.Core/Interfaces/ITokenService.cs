using SecurityRecap.Core.Entities;

namespace SecurityRecap.Core.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user);
    string GenerateRefreshToken();
    Guid? ValidateRefreshToken(string token);
}
