using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Zephyrus.Api.Authentication;

/// <summary>
/// How many wrong team tokens one client may present before it is stopped.
/// </summary>
public sealed class FailedSignInOptions
{
    public const string SectionName = "Authentication:FailedSignIns";

    /// <summary>Wrong tokens allowed per client within <see cref="Window"/>.</summary>
    public int Limit { get; set; } = 10;

    /// <summary>
    /// The counting window. A client that reaches the limit is blocked until
    /// its window ends.
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Counts wrong team tokens per client in fixed windows. Team tokens are long
/// random secrets, so guessing one is already impractical; this stops a client
/// from trying at volume, and makes an attempt visible in the logs.
/// </summary>
/// <remarks>
/// Counts are kept in memory, per API instance, and reset on restart. Clients
/// are told apart by IP address: behind a reverse proxy every caller shares the
/// proxy's address unless forwarded headers are configured for that proxy.
/// </remarks>
public sealed class FailedSignInLimiter
{
    // Bounds memory if many distinct addresses fail; expired windows are
    // dropped first.
    private const int MaxTrackedClients = 10_000;

    private readonly ConcurrentDictionary<string, Window> _clients = new();
    private readonly FailedSignInOptions _options;
    private readonly TimeProvider _time;

    public FailedSignInLimiter(IOptions<FailedSignInOptions> options, TimeProvider time)
    {
        _options = options.Value;
        _time = time;
    }

    /// <summary>The address failed sign-ins are counted against.</summary>
    public static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// Whether <paramref name="client"/> has used up its wrong tokens, and if
    /// so, how long until it may try again.
    /// </summary>
    public bool IsBlocked(string client, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (!_clients.TryGetValue(client, out var window))
            return false;

        var remaining = window.Start + _options.Window - _time.GetUtcNow();
        if (remaining <= TimeSpan.Zero || window.Failures < _options.Limit)
            return false;

        retryAfter = remaining;
        return true;
    }

    /// <summary>Records one wrong token from <paramref name="client"/>.</summary>
    public void RecordFailure(string client)
    {
        var now = _time.GetUtcNow();
        if (_clients.Count >= MaxTrackedClients)
            RemoveExpired(now);

        _clients.AddOrUpdate(
            client,
            _ => new Window(now, 1),
            (_, window) => window.Start + _options.Window <= now
                ? new Window(now, 1)
                : window with { Failures = window.Failures + 1 });
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var (client, window) in _clients)
        {
            if (window.Start + _options.Window <= now)
                _clients.TryRemove(client, out _);
        }
    }

    private sealed record Window(DateTimeOffset Start, int Failures);
}
