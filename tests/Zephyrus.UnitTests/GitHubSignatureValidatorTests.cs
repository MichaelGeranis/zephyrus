using System.Security.Cryptography;
using System.Text;
using Zephyrus.Api.Webhooks;

namespace Zephyrus.UnitTests;

public class GitHubSignatureValidatorTests
{
    private const string Secret = "shhh";

    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"action":"closed"}""");

    private static string Sign(string secret, byte[] body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }

    [Fact]
    public void IsValid_WhenSignatureMatches_ShouldReturnTrue()
    {
        Assert.True(GitHubSignatureValidator.IsValid(Secret, Sign(Secret, Body), Body));
    }

    [Fact]
    public void IsValid_WhenSignedWithAnotherSecret_ShouldReturnFalse()
    {
        Assert.False(GitHubSignatureValidator.IsValid(Secret, Sign("wrong", Body), Body));
    }

    [Fact]
    public void IsValid_WhenBodyIsTampered_ShouldReturnFalse()
    {
        var signature = Sign(Secret, Body);
        var tampered = Encoding.UTF8.GetBytes("""{"action":"opened"}""");

        Assert.False(GitHubSignatureValidator.IsValid(Secret, signature, tampered));
    }

    [Fact]
    public void IsValid_WhenNoSecretIsConfigured_ShouldReturnFalse()
    {
        // Failing closed matters: an unconfigured secret must not accept traffic.
        Assert.False(GitHubSignatureValidator.IsValid("", Sign("", Body), Body));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("deadbeef")]
    [InlineData("sha1=deadbeef")]
    public void IsValid_WhenHeaderIsMissingOrMalformed_ShouldReturnFalse(string? header)
    {
        Assert.False(GitHubSignatureValidator.IsValid(Secret, header, Body));
    }
}
