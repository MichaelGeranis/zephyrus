using System.Security.Cryptography;

namespace Zephyrus.Infrastructure.GitHub;

/// <summary>
/// The Zephyrus GitHub App. When configured, a repository the App is installed
/// on is accessed with short-lived installation tokens instead of the
/// project's stored token.
/// </summary>
public sealed class GitHubAppOptions
{
    public const string SectionName = "GitHub:App";

    /// <summary>The App ID from the App's settings page.</summary>
    public long? AppId { get; set; }

    /// <summary>The App's private key (PEM). Supply through a secret, never commit it.</summary>
    public string? PrivateKey { get; set; }

    /// <summary>A file holding the private key, instead of <see cref="PrivateKey"/>.</summary>
    public string? PrivateKeyPath { get; set; }

    public bool IsConfigured => AppId is not null;

    /// <summary>Checks the key at startup when the App is configured.</summary>
    public void LoadPrivateKeyIfConfigured()
    {
        if (IsConfigured)
            LoadPrivateKey().Dispose();
    }

    /// <summary>
    /// Loads the private key. Throws at startup on a missing or unreadable key,
    /// rather than on the first agent run.
    /// </summary>
    public RSA LoadPrivateKey()
    {
        var pem = !string.IsNullOrWhiteSpace(PrivateKey)
            ? PrivateKey.Replace("\\n", "\n")
            : !string.IsNullOrWhiteSpace(PrivateKeyPath)
                ? File.ReadAllText(PrivateKeyPath)
                : throw new InvalidOperationException("GitHub:App:AppId is set, so GitHub:App:PrivateKey or PrivateKeyPath is required.");

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
        }
        catch (ArgumentException ex)
        {
            rsa.Dispose();
            throw new InvalidOperationException("GitHub:App private key is not a valid PEM RSA key.", ex);
        }
        return rsa;
    }
}
