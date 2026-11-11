namespace Zephyrus.Infrastructure.Jobs;

/// <summary>
/// Wakes the worker as soon as a job is queued, so it does not wait for the
/// next poll. Polling still covers retries and jobs queued by other instances.
/// </summary>
public sealed class AgentJobSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        // At most one pending wake-up: the worker drains every due job per wake.
        if (_signal.CurrentCount == 0)
        {
            try { _signal.Release(); }
            catch (SemaphoreFullException) { }
        }
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        await _signal.WaitAsync(timeout, ct);
    }
}
