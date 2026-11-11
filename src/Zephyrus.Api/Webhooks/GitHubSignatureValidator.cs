using System.Security.Cryptography;
using System.Text;

namespace Zephyrus.Api.Webhooks;

/// <summary>
/// Verifies the <c>X-Hub-Signature-256</c> header GitHub sends with every
/// delivery. This is the only thing authenticating a webhook — the endpoint is
/// public, so an unverified body must never be acted on.
/// </summary>
public static class GitHubSignatureValidator
{
    public const string HeaderName = "X-Hub-Signature-256";

    private const string Prefix = "sha256=";

    public static bool IsValid(string secret, string? signatureHeader, byte[] body)
    {
        if (string.IsNullOrEmpty(secret))
            return false;

        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        if (!signatureHeader.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
        var provided = signatureHeader[Prefix.Length..].Trim().ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided));
    }
}
