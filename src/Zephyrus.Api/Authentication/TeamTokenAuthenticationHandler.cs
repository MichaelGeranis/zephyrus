using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Zephyrus.Api.Authentication;

/// <summary>
/// Authenticates a caller from an <c>Authorization: Bearer &lt;token&gt;</c>
/// header against the configured team roster, and turns the matched member
/// into a principal carrying their role claims.
/// </summary>
public sealed class TeamTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TeamToken";

    private const string HeaderPrefix = "Bearer ";

    private readonly TeamOptions _team;

    public TeamTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<TeamOptions> team)
        : base(options, logger, encoder)
    {
        _team = team.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
            return Task.FromResult(AuthenticateResult.NoResult());

        var value = header.ToString();
        if (!value.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = value[HeaderPrefix.Length..].Trim();
        if (token.Length == 0)
            return Task.FromResult(AuthenticateResult.Fail("Bearer token was empty."));

        var member = _team.Members.FirstOrDefault(m => TokenMatches(m.Token, token));
        if (member is null)
            return Task.FromResult(AuthenticateResult.Fail("Bearer token did not match any team member."));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, member.Email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(member.DisplayName) ? member.Email : member.DisplayName),
        };
        claims.AddRange(member.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    /// <summary>
    /// Compares in constant time so a token cannot be recovered by timing the
    /// comparison. Differing lengths short-circuit, which only leaks length.
    /// </summary>
    private static bool TokenMatches(string configured, string provided)
    {
        if (string.IsNullOrEmpty(configured))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(configured),
            Encoding.UTF8.GetBytes(provided));
    }
}
