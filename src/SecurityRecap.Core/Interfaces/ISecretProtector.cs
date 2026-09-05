namespace SecurityRecap.Core.Interfaces;

/// <summary>
/// Reversible protection for integration secrets that must be replayed to a third party
/// (unlike user passwords or API keys, which are one-way hashed).
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    /// <summary>
    /// Reverses <see cref="Protect"/>. Throws <see cref="System.Security.Cryptography.CryptographicException"/>
    /// if the payload was protected with a key ring this instance cannot read.
    /// </summary>
    string Unprotect(string protectedValue);
}
