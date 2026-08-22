namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Encrypts secrets that must not be readable in the database — currently the
/// per-project code-host token.
/// </summary>
/// <remarks>
/// Applied at the persistence boundary, so entities always hold the plaintext
/// in memory and only the stored form is encrypted.
/// </remarks>
public interface ISecretProtector
{
    /// <summary>Returns the stored form of <paramref name="plaintext"/>.</summary>
    string Protect(string plaintext);

    /// <summary>
    /// Returns the plaintext behind a stored value. Values written before
    /// encryption was introduced are returned unchanged.
    /// </summary>
    string Unprotect(string stored);
}
