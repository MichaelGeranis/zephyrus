namespace Zephyrus.Infrastructure.Jobs;

/// <summary>
/// Timing for the durable agent job queue.
/// </summary>
public sealed class AgentJobQueueOptions
{
    public const string SectionName = "AgentJobs";

    /// <summary>
    /// How often the worker looks for due jobs when it has not been woken by a
    /// new one: retries that came due, and jobs queued by another instance.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a worker may hold a job before another may take it over. Longer
    /// than the slowest agent run: the Code Agent makes several passes per task.
    /// </summary>
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>How long a failed job waits before its retry.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);
}
