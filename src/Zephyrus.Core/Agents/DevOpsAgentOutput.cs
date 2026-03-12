namespace Zephyrus.Core.Agents;

/// <summary>
/// Output from the DevOps Agent: a GitHub Actions workflow file.
/// </summary>
public sealed class DevOpsAgentOutput
{
    public required string WorkflowYaml { get; init; }
    public required string RepositoryPath { get; init; }
}
