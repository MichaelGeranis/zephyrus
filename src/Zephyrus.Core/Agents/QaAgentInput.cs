namespace Zephyrus.Core.Agents;

/// <summary>
/// Input for the QA Agent: the PR details, task context, and project constitution.
/// </summary>
public sealed class QaAgentInput
{
    public required string FeatureSlug { get; init; }
    public required string ApprovedAdr { get; init; }
    public required string ProjectConstitution { get; init; }
    public required IReadOnlyList<QaTaskContext> Tasks { get; init; }
}

/// <summary>
/// Context for a single task's PR, used as input to the QA Agent.
/// </summary>
public sealed class QaTaskContext
{
    public required string TaskTitle { get; init; }
    public required int PrId { get; init; }
    public required string BranchName { get; init; }
}
