using System.Security.Cryptography;
using System.Text;

namespace Zephyrus.Api.Authentication;

/// <summary>
/// The team roster. Zephyrus is built for a small, fixed team, so members are
/// configured rather than self-registered.
/// </summary>
public sealed class TeamOptions
{
    public const string SectionName = "Team";

    /// <summary>Prefix of the public tokens used by the Docker demo setup.</summary>
    public const string DemoTokenPrefix = "demo-";

    public List<TeamMemberOptions> Members { get; set; } = new();
}

/// <summary>
/// One team member and the token that authenticates them.
/// </summary>
public sealed class TeamMemberOptions
{
    /// <summary>Identifies the member; recorded as the approver on artifacts.</summary>
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Bearer token presented by this member, as written. Supply via configuration
    /// or an environment variable — never commit a real one. Prefer
    /// <see cref="TokenHash"/>, so the configuration never holds a usable token.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 of the member's token, as 64 hexadecimal characters
    /// (<c>printf %s "$TOKEN" | sha256sum</c>). When set, it is used instead of
    /// <see cref="Token"/>: someone who reads the configuration cannot sign in with it.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>True when the member is configured with a hash rather than the token itself.</summary>
    public bool UsesTokenHash => !string.IsNullOrEmpty(TokenHash);

    /// <summary>True when <see cref="TokenHash"/> is empty or 64 hexadecimal characters.</summary>
    public bool HasValidTokenHash => !UsesTokenHash || (TokenHash.Length == 64 && TokenHash.All(Uri.IsHexDigit));

    /// <summary>
    /// Whether <paramref name="presented"/> is this member's token. Compares in
    /// constant time so it cannot be recovered by timing; a member with neither a
    /// token nor a hash never matches.
    /// </summary>
    public bool Matches(string presented)
    {
        if (UsesTokenHash)
        {
            if (!HasValidTokenHash)
                return false;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
            return CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(TokenHash));
        }

        if (string.IsNullOrEmpty(Token))
            return false;

        // Differing lengths short-circuit, which only leaks the length
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Token), Encoding.UTF8.GetBytes(presented));
    }

    /// <summary>Role names, parsed against <c>TeamRole</c>.</summary>
    public List<string> Roles { get; set; } = new();
}
