using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zephyrus.Infrastructure.Persistence;

namespace Zephyrus.Infrastructure.Jobs;

/// <summary>
/// Runs <see cref="AgentRunContentMaintenance"/> shortly after startup and then
/// once a day, off the request path. A failure is logged and retried at the next
/// run; it never stops the API.
/// </summary>
public sealed class AgentRunContentMaintenanceService : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AgentRunContentMaintenanceService> _logger;

    public AgentRunContentMaintenanceService(IServiceScopeFactory scopes, ILogger<AgentRunContentMaintenanceService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var maintenance = scope.ServiceProvider.GetRequiredService<AgentRunContentMaintenance>();
            await maintenance.EncryptStoredContentAsync(ct);
            await maintenance.RemoveExpiredContentAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Agent run content maintenance failed; it will run again tomorrow");
        }
    }
}
