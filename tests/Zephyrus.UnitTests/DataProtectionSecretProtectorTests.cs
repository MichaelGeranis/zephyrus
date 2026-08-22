using Microsoft.AspNetCore.DataProtection;
using Zephyrus.Infrastructure.Security;

namespace Zephyrus.UnitTests;

public class DataProtectionSecretProtectorTests
{
    private readonly DataProtectionSecretProtector _sut =
        new(new EphemeralDataProtectionProvider());

    private const string Token = "ghp_exampleTokenValue0123456789abcdefghij";

    [Fact]
    public void Protect_WhenGivenASecret_ShouldNotReturnItInPlaintext()
    {
        var stored = _sut.Protect(Token);

        Assert.DoesNotContain(Token, stored, StringComparison.Ordinal);
    }

    [Fact]
    public void Protect_WhenGivenASecret_ShouldMarkTheStoredValueAsEncrypted()
    {
        Assert.StartsWith(DataProtectionSecretProtector.Prefix, _sut.Protect(Token));
    }

    [Fact]
    public void Unprotect_WhenGivenAProtectedValue_ShouldReturnTheOriginalSecret()
    {
        Assert.Equal(Token, _sut.Unprotect(_sut.Protect(Token)));
    }

    [Fact]
    public void Protect_WhenCalledTwice_ShouldNotProduceTheSameCiphertext()
    {
        // Deterministic output would let anyone with database access tell which
        // projects share a token.
        Assert.NotEqual(_sut.Protect(Token), _sut.Protect(Token));
    }

    [Fact]
    public void Unprotect_WhenValueWasStoredBeforeEncryption_ShouldReturnItUnchanged()
    {
        // Rows written by the old scheme carry no prefix and must keep working.
        Assert.Equal(Token, _sut.Unprotect(Token));
    }

    [Fact]
    public void Unprotect_WhenProtectedValueIsTampered_ShouldThrow()
    {
        var stored = _sut.Protect(Token);
        var tampered = stored[..^4] + "AAAA";

        Assert.ThrowsAny<Exception>(() => _sut.Unprotect(tampered));
    }

    [Fact]
    public void Unprotect_WhenProtectedByAnotherKeyRing_ShouldThrowRatherThanReturnCiphertext()
    {
        // A lost key ring must fail loudly, not hand ciphertext to the code host.
        var otherRing = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());

        Assert.ThrowsAny<Exception>(() => _sut.Unprotect(otherRing.Protect(Token)));
    }

    [Theory]
    [InlineData("")]
    public void Protect_WhenSecretIsEmpty_ShouldReturnItUnchanged(string value)
    {
        Assert.Equal(value, _sut.Protect(value));
        Assert.Equal(value, _sut.Unprotect(value));
    }
}
