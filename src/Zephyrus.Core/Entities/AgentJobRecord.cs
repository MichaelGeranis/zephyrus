using Zephyrus.Core.Enums;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Core.Entities;

/// <summary>
/// A stored agent job. Keeping jobs in the database means they survive a
/// restart, are retried, and their progress can be shown to the team.
/// </summary>
/// <remarks>
/// <para>
/// Retry policy (BUSINESS.md, "Error Handling and Retry Policy"): a failed job
/// is retried once, then marked <see cref="AgentJobStatus.Failed"/> and left
/// for a person to re-run. It is never retried indefinitely.
/// </para>
/// <para>
/// A worker that claims a job holds a lease until <see cref="LeaseExpiresAt"/>.
/// If the process stops mid-job the lease runs out and another worker can claim
/// it again, which counts as another attempt.
/// </para>
/// </remarks>
public class AgentJobRecord
{
    /// <summary>The first attempt plus one retry.</summary>
    public const int MaxAttempts = 2;

    private const int MaxErrorLength = 2000;

    public Guid Id { get; private set; }
    public Guid FeatureId { get; private set; }
    public AgentJobKind Kind { get; private set; }

    /// <summary>Re-run requested by a person; see <see cref="AgentJob.ForceRerun"/>.</summary>
    public bool ForceRerun { get; private set; }
    public AgentJobStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime EnqueuedAt { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Changes on every state change, so two workers cannot both claim the same
    /// job: the second save sees a different value and is rejected.
    /// </summary>
    public Guid ConcurrencyStamp { get; private set; }

    /// <summary>
    /// W3C trace context of the request that queued the job, so the job's work
    /// shows up in the same trace even though it runs later.
    /// </summary>
    public string? TraceParent { get; private set; }

    private AgentJobRecord() { }

    public static AgentJobRecord Create(AgentJob job, DateTime now, string? traceParent = null)
    {
        return new AgentJobRecord
        {
            Id = Guid.NewGuid(),
            FeatureId = job.FeatureId,
            Kind = job.Kind,
            ForceRerun = job.ForceRerun,
            Status = AgentJobStatus.Queued,
            EnqueuedAt = now,
            NextAttemptAt = now,
            ConcurrencyStamp = Guid.NewGuid(),
            TraceParent = traceParent
        };
    }

    public AgentJob ToJob() => new(FeatureId, Kind, ForceRerun);

    /// <summary>Waiting for a worker, or being worked on.</summary>
    public bool IsActive => Status is AgentJobStatus.Queued or AgentJobStatus.Running;

    /// <summary>
    /// A job can be claimed when it is queued and due, or when the worker
    /// running it has let its lease expire.
    /// </summary>
    public bool IsClaimable(DateTime now) =>
        (Status == AgentJobStatus.Queued && NextAttemptAt <= now)
        || (Status == AgentJobStatus.Running && LeaseExpiresAt < now);

    /// <summary>
    /// Starts an attempt. Returns <c>false</c> when the job cannot run: either
    /// it is not claimable, or its lease expired with no attempts left, in
    /// which case it is marked failed.
    /// </summary>
    public bool TryClaim(DateTime now, TimeSpan lease)
    {
        if (!IsClaimable(now))
            return false;

        if (Status == AgentJobStatus.Running && Attempts >= MaxAttempts)
        {
            Finish(AgentJobStatus.Failed, now,
                "The job was interrupted before it finished and has no attempts left. Re-run the step to try again.");
            return false;
        }

        Status = AgentJobStatus.Running;
        Attempts++;
        StartedAt = now;
        LeaseExpiresAt = now + lease;
        ConcurrencyStamp = Guid.NewGuid();
        return true;
    }

    public void Succeed(DateTime now)
    {
        EnsureRunning();
        Finish(AgentJobStatus.Succeeded, now, error: null);
    }

    /// <summary>
    /// Records a failed attempt: queues the job again after
    /// <paramref name="retryDelay"/> if it has an attempt left, or marks it
    /// failed.
    /// </summary>
    public void Fail(string error, DateTime now, TimeSpan retryDelay)
    {
        EnsureRunning();

        if (Attempts < MaxAttempts)
        {
            Status = AgentJobStatus.Queued;
            LastError = Truncate(error);
            NextAttemptAt = now + retryDelay;
            LeaseExpiresAt = null;
            ConcurrencyStamp = Guid.NewGuid();
            return;
        }

        Finish(AgentJobStatus.Failed, now, error);
    }

    private void Finish(AgentJobStatus status, DateTime now, string? error)
    {
        Status = status;
        LastError = error is null ? null : Truncate(error);
        CompletedAt = now;
        LeaseExpiresAt = null;
        ConcurrencyStamp = Guid.NewGuid();
    }

    private void EnsureRunning()
    {
        if (Status != AgentJobStatus.Running)
            throw new InvalidOperationException($"Agent job {Id} is {Status}, not running.");
    }

    private static string Truncate(string error) =>
        error.Length <= MaxErrorLength ? error : error[..MaxErrorLength] + "…";
}
