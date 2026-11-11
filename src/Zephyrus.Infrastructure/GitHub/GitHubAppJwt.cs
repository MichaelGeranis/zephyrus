using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Zephyrus.Infrastructure.GitHub;

/// <summary>
/// The JSON Web Token a GitHub App signs to authenticate as itself (RS256).
/// It is only used to find installations and mint installation tokens.
/// </summary>
public static class GitHubAppJwt
{
    // GitHub accepts at most ten minutes; issued a minute early for clock drift.
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(9);
    private static readonly TimeSpan Backdate = TimeSpan.FromSeconds(60);

    public static string Create(long appId, RSA key, DateTimeOffset now)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = (now - Backdate).ToUnixTimeSeconds(),
            exp = (now + Lifetime).ToUnixTimeSeconds(),
            iss = appId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }));

        var signed = $"{header}.{payload}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signed}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
