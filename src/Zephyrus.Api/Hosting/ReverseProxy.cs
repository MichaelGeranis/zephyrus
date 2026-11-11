using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Zephyrus.Api.Hosting;

/// <summary>
/// Running behind a reverse proxy (nginx, Caddy, a cloud load balancer). The
/// proxy terminates TLS and every request arrives from its address, so the
/// API reads the client's address and scheme from X-Forwarded-For and
/// X-Forwarded-Proto. The failed sign-in limit counts per client address and
/// depends on this: without it, one wrong token blocks everyone.
/// </summary>
/// <remarks>
/// The headers are trusted only from the proxies listed in
/// <c>ReverseProxy:KnownProxies</c> (addresses) or
/// <c>ReverseProxy:KnownNetworks</c> (CIDR ranges). From anyone else they are
/// ignored, so a client cannot pick its own address to dodge the limit.
/// </remarks>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    public bool Enabled { get; set; }
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>Configures forwarded headers; returns false when they are off.</summary>
    public static bool Configure(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<ReverseProxyOptions>() ?? new ReverseProxyOptions();
        if (!options.Enabled)
            return false;

        if (options.KnownProxies.Length == 0 && options.KnownNetworks.Length == 0)
            throw new InvalidOperationException(
                "ReverseProxy:Enabled is true but no ReverseProxy:KnownProxies or ReverseProxy:KnownNetworks are set. " +
                "List the proxy's address so forwarded headers from anyone else are ignored.");

        services.Configure<ForwardedHeadersOptions>(forwarded =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Only the nearest proxy's entry is used; earlier ones are client-supplied.
            forwarded.ForwardLimit = 1;
            forwarded.KnownProxies.Clear();
            forwarded.KnownIPNetworks.Clear();
            foreach (var proxy in options.KnownProxies)
                forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
            foreach (var network in options.KnownNetworks)
                forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });
        return true;
    }
}
