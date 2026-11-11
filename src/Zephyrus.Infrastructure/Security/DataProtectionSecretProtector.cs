using Microsoft.AspNetCore.DataProtection;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Security;

/// <summary>
/// <see cref="ISecretProtector"/> backed by ASP.NET Core Data Protection.
/// </summary>
/// <remarks>
/// Protected values carry a version prefix. That is what lets a database
/// written before this change keep working: a stored value without the prefix
/// is a plaintext token from the old scheme and is returned as-is, then written
/// back encrypted the next time its project is saved. It also means a genuine
/// decryption failure surfaces as an error rather than silently handing
/// ciphertext to the code host as if it were a token.
/// </remarks>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    /// <summary>Marks a value as encrypted by this class, and with which scheme.</summary>
    public const string Prefix = "enc:v1:";

    private const string Purpose = "Zephyrus.ProjectSecrets.v1";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        return Prefix + _protector.Protect(plaintext);
    }

    public string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored))
            return stored;

        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored;

        return _protector.Unprotect(stored[Prefix.Length..]);
    }
}
