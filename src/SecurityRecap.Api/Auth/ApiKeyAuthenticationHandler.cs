using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Api.Auth;

public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions { }

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly IApiKeyService _apiKeys;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeys)
        : base(options, logger, encoder)
    {
        _apiKeys = apiKeys;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
            return AuthenticateResult.NoResult();

        var raw = values.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return AuthenticateResult.NoResult();

        var result = await _apiKeys.ValidateAndTouchAsync(raw);
        if (result is null)
            return AuthenticateResult.Fail("Invalid or revoked API key.");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, result.ApiKeyId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, result.ApiKeyId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, $"apikey:{result.Name}"),
            new Claim("tenant_id", result.TenantId.ToString()),
            new Claim("role", nameof(UserRole.Admin)),
            new Claim("full_name", $"API Key ({result.Name})"),
            new Claim("auth_kind", "api_key")
        };

        var identity = new ClaimsIdentity(claims, SchemeName, JwtRegisteredClaimNames.Sub, "role");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
