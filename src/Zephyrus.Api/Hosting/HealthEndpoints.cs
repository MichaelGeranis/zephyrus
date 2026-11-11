using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zephyrus.Infrastructure.Persistence;

namespace Zephyrus.Api.Hosting;

/// <summary>
/// Health endpoints for container orchestrators and load balancers. Both are
/// anonymous and say nothing beyond up or down, the version and, for
/// readiness, which check failed.
/// </summary>
/// <remarks>
/// <c>/health</c> is liveness: the process answers. It runs no checks, so a
/// database outage does not get the API restarted. <c>/health/ready</c> is
/// readiness: the database can be reached too.
/// </remarks>
public static class HealthEndpoints
{
    public const string LivePath = "/health";
    public const string ReadyPath = "/health/ready";
    private const string ReadyTag = "ready";

    /// <summary>The running version, e.g. "0.1.0", without the build's commit suffix.</summary>
    public static string Version { get; } =
        (typeof(HealthEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown").Split('+')[0];

    public static IServiceCollection AddZephyrusHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag]);
        return services;
    }

    public static void MapZephyrusHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteAsync,
        }).AllowAnonymous();

        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteAsync,
        }).AllowAnonymous();
    }

    private static Task WriteAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            version = Version,
            checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString()),
        });

    private sealed class DatabaseHealthCheck(DatabaseHealthProbe probe) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                return await probe.CanConnectAsync(ct)
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("The database cannot be reached.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("The database cannot be reached.", ex);
            }
        }
    }
}
