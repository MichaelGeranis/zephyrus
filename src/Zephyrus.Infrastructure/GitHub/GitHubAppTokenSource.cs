using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zephyrus.Infrastructure.GitHub;

/// <summary>
/// Installation tokens for repositories the Zephyrus GitHub App is installed
/// on. Tokens last an hour; one is reused until five minutes before it
/// expires. A repository without the App is remembered for a few minutes, so
/// its project's stored token is used without asking GitHub each time.
/// </summary>
public sealed class GitHubAppTokenSource : IDisposable
{
    private static readonly TimeSpan RefreshBefore = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan NotInstalledFor = TimeSpan.FromMinutes(5);

    private readonly GitHubAppOptions _options;
    private readonly RSA? _key;
    private readonly IGitHubAppApi _api;
    private readonly TimeProvider _time;
    private readonly ILogger<GitHubAppTokenSource> _logger;
    private readonly ConcurrentDictionary<string, Entry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public GitHubAppTokenSource(
        IOptions<GitHubAppOptions> options,
        IGitHubAppApi api,
        TimeProvider time,
        ILogger<GitHubAppTokenSource> logger)
    {
        _options = options.Value;
        _key = _options.IsConfigured ? _options.LoadPrivateKey() : null;
        _api = api;
        _time = time;
        _logger = logger;
    }

    public bool IsConfigured => _key is not null;

    /// <summary>
    /// A token for <paramref name="owner"/>/<paramref name="repo"/>, or null
    /// when the App is not configured, not installed there, or unreachable.
    /// </summary>
    public async Task<string?> TokenForAsync(string owner, string repo, CancellationToken ct = default)
    {
        if (_key is null)
            return null;

        var key = $"{owner}/{repo}";
        var now = _time.GetUtcNow();
        if (_cache.TryGetValue(key, out var cached) && cached.ValidUntil > now)
            return cached.Token;

        try
        {
            var jwt = GitHubAppJwt.Create(_options.AppId!.Value, _key, now);
            var installation = await _api.FindInstallationAsync(owner, repo, jwt, ct);
            if (installation is null)
            {
                _cache[key] = new Entry(null, now + NotInstalledFor);
                return null;
            }

            var access = await _api.CreateInstallationTokenAsync(installation.Value, jwt, ct);
            _cache[key] = new Entry(access.Token, access.ExpiresAt - RefreshBefore);
            return access.Token;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Misconfigured App or GitHub unavailable: say so, and let the
            // project's own token be tried rather than fail the agent run here.
            _logger.LogWarning(ex, "GitHub App token for {Repository} failed; falling back to the project's token.", key);
            return null;
        }
    }

    public void Dispose() => _key?.Dispose();

    private sealed record Entry(string? Token, DateTimeOffset ValidUntil);
}
