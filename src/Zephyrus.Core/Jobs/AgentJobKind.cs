namespace Zephyrus.Core.Jobs;

/// <summary>
/// Identifies which agent an <see cref="AgentJob"/> should invoke.
/// Mirrors the orchestrator trigger map: each pipeline status that
/// automatically triggers an agent maps to exactly one kind.
/// </summary>
public enum AgentJobKind
{
    Architect,
    Task,
    Code,
    Qa,
    DevOps
}
