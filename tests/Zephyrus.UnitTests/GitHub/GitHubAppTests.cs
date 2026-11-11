using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zephyrus.Infrastructure.GitHub;
using Zephyrus.UnitTests.Jobs;

namespace Zephyrus.UnitTests.GitHub;

public class GitHubAppJwtTests
{
    private static JsonElement Decode(string part)
    {
        var padded = part.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return JsonDocument.Parse(Convert.FromBase64String(padded)).RootElement;
    }

    private static byte[] Bytes(string part)
    {
        var padded = part.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded + new string('=', (4 - padded.Length % 4) % 4));
    }

    [Fact]
    public void Create_ShouldSignAnRs256TokenIssuedByTheAppThatGitHubAccepts()
    {
        using var key = RSA.Create(2048);
        var now = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

        var parts = GitHubAppJwt.Create(12345, key, now).Split('.');

        Assert.Equal(3, parts.Length);
        Assert.Equal("RS256", Decode(parts[0]).GetProperty("alg").GetString());
        var payload = Decode(parts[1]);
        Assert.Equal("12345", payload.GetProperty("iss").GetString());
        Assert.Equal(now.AddSeconds(-60).ToUnixTimeSeconds(), payload.GetProperty("iat").GetInt64());
        Assert.True(payload.GetProperty("exp").GetInt64() - now.ToUnixTimeSeconds() <= 600); // GitHub's ten-minute limit
        Assert.True(key.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Bytes(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }
}

public class GitHubAppTokenSourceTests
{
    private readonly ManualTimeProvider _clock = new(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc));
    private readonly FakeGitHubAppApi _api = new();

    private GitHubAppTokenSource Create(bool configured = true)
    {
        using var key = RSA.Create(2048);
        var options = configured
            ? new GitHubAppOptions { AppId = 12345, PrivateKey = key.ExportRSAPrivateKeyPem() }
            : new GitHubAppOptions();
        return new GitHubAppTokenSource(Options.Create(options), _api, _clock, NullLogger<GitHubAppTokenSource>.Instance);
    }

    [Fact]
    public async Task TokenForAsync_WhenTheAppIsNotConfigured_ShouldReturnNullWithoutCallingGitHub()
    {
        using var source = Create(configured: false);

        Assert.Null(await source.TokenForAsync("org", "repo", Ct));
        Assert.Equal(0, _api.Calls);
    }

    [Fact]
    public async Task TokenForAsync_WhenTheAppIsInstalled_ShouldReturnAnInstallationTokenAndReuseIt()
    {
        _api.Installations["org/repo"] = 77;
        using var source = Create();

        Assert.Equal("token-77-1", await source.TokenForAsync("org", "repo", Ct));
        _clock.Advance(TimeSpan.FromMinutes(50));
        Assert.Equal("token-77-1", await source.TokenForAsync("org", "repo", Ct));
        Assert.Equal(1, _api.TokensCreated);
    }

    [Fact]
    public async Task TokenForAsync_WhenTheTokenIsAboutToExpire_ShouldMintANewOne()
    {
        _api.Installations["org/repo"] = 77;
        using var source = Create();
        await source.TokenForAsync("org", "repo", Ct);

        _clock.Advance(TimeSpan.FromMinutes(56)); // tokens last an hour; refreshed five minutes early

        Assert.Equal("token-77-2", await source.TokenForAsync("org", "repo", Ct));
    }

    [Fact]
    public async Task TokenForAsync_WhenTheAppIsNotInstalled_ShouldReturnNullAndRememberItForAWhile()
    {
        using var source = Create();

        Assert.Null(await source.TokenForAsync("org", "other", Ct));
        Assert.Null(await source.TokenForAsync("org", "other", Ct));
        Assert.Equal(1, _api.Lookups);

        _api.Installations["org/other"] = 88; // installed since
        _clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal("token-88-1", await source.TokenForAsync("org", "other", Ct));
    }

    [Fact]
    public async Task TokenForAsync_WhenGitHubFails_ShouldReturnNullSoTheProjectTokenIsUsed()
    {
        _api.Fail = true;
        using var source = Create();

        Assert.Null(await source.TokenForAsync("org", "repo", Ct));
    }

    [Fact]
    public void Constructor_WhenThePrivateKeyIsInvalid_ShouldThrow()
    {
        var options = new GitHubAppOptions { AppId = 1, PrivateKey = "not a key" };

        Assert.Throws<InvalidOperationException>(() =>
            new GitHubAppTokenSource(Options.Create(options), _api, _clock, NullLogger<GitHubAppTokenSource>.Instance));
    }

    [Fact]
    public async Task CodeHost_WhenTheAppIsInstalled_ShouldUseItsTokenOverTheProjects()
    {
        _api.Installations["org/repo"] = 77;
        using var source = Create();

        Assert.Equal("token-77-1", await new GitHubCodeHost("ghp_project", source).TokenForAsync("org", "repo", Ct));
        Assert.Equal("ghp_project", await new GitHubCodeHost("ghp_project", source).TokenForAsync("org", "elsewhere", Ct));
    }

    [Fact]
    public async Task CodeHost_WhenThereAreNoCredentials_ShouldSaySoBeforeCallingGitHub()
    {
        using var source = Create(configured: false);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GitHubCodeHost("", source).GetIssueContentAsync("org/repo", 1, Ct));

        Assert.Contains("install the Zephyrus GitHub App", error.Message);
    }

    private sealed class FakeGitHubAppApi : IGitHubAppApi
    {
        public Dictionary<string, long> Installations { get; } = new();
        public bool Fail { get; set; }
        public int Lookups { get; private set; }
        public int TokensCreated { get; private set; }
        public int Calls => Lookups + TokensCreated;
        private readonly Dictionary<long, int> _issued = new();
        private DateTimeOffset _now;

        public Task<long?> FindInstallationAsync(string owner, string repo, string appJwt, CancellationToken ct = default)
        {
            Lookups++;
            if (Fail)
                throw new HttpRequestException("GitHub is down");
            Assert.Equal(3, appJwt.Split('.').Length);
            _now = DateTimeOffset.FromUnixTimeSeconds(
                JsonDocument.Parse(Convert.FromBase64String(Pad(appJwt.Split('.')[1]))).RootElement.GetProperty("iat").GetInt64() + 60);
            return Task.FromResult(Installations.TryGetValue($"{owner}/{repo}", out var id) ? id : (long?)null);
        }

        public Task<InstallationAccess> CreateInstallationTokenAsync(long installationId, string appJwt, CancellationToken ct = default)
        {
            TokensCreated++;
            _issued[installationId] = _issued.GetValueOrDefault(installationId) + 1;
            return Task.FromResult(new InstallationAccess($"token-{installationId}-{_issued[installationId]}", _now.AddHours(1)));
        }

        private static string Pad(string part)
        {
            var s = part.Replace('-', '+').Replace('_', '/');
            return s + new string('=', (4 - s.Length % 4) % 4);
        }
    }
}

public class GitHubAppOptionsTests
{
    [Fact]
    public void Bind_WhenTheAppIdIsEmpty_ShouldLeaveTheAppUnconfigured()
    {
        // docker-compose passes GitHub__App__AppId through even when unset.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GitHub:App:AppId"] = "", ["GitHub:App:PrivateKey"] = "" })
            .Build();

        var options = configuration.GetSection(GitHubAppOptions.SectionName).Get<GitHubAppOptions>()!;

        Assert.False(options.IsConfigured);
    }
}
