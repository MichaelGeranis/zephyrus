namespace Zephyrus.Core.Jobs;

/// <summary>
/// Identifies which agent an <see cref="AgentJob"/> should invoke. The
/// orchestrator's trigger map queues all but <see cref="Prd"/>, which a person
/// requests; any kind can also be queued as a re-run.
/// </summary>
public enum AgentJobKind
{
    Prd,
    Architect,
    Task,
    Code,
    Qa,
    DevOps
}
