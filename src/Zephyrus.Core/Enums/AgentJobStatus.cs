namespace Zephyrus.Core.Enums;

/// <summary>
/// Where a queued agent job is in its life.
/// </summary>
public enum AgentJobStatus
{
    /// <summary>Waiting for a worker, either new or waiting to retry.</summary>
    Queued,

    /// <summary>Claimed by a worker that holds its lease.</summary>
    Running,

    /// <summary>The agent finished.</summary>
    Succeeded,

    /// <summary>Every attempt failed. A person has to re-run the step.</summary>
    Failed
}
