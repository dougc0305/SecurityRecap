using Microsoft.AspNetCore.DataProtection;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Infrastructure.Services;

public class DataProtectionSecretProtector : ISecretProtector
{
    // Changing this purpose string makes every previously stored secret undecryptable.
    private const string Purpose = "SecurityRecap.IntegrationSecrets.v1";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
