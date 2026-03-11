namespace Zephyrus.Core.Agents;

/// <summary>
/// Input for the Code Agent: a single task to implement, plus the ADR
/// and project constitution for context.
/// </summary>
public sealed class CodeAgentInput
{
    public required string TaskTitle { get; init; }
    public required string TaskBody { get; init; }
    public required string ApprovedAdr { get; init; }
    public required string ProjectConstitution { get; init; }
    public required string FeatureSlug { get; init; }
    public required string BranchName { get; init; }
}
