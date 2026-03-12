namespace Zephyrus.Core.Agents;

/// <summary>
/// Input for the DevOps Agent: project constitution and deployment context.
/// </summary>
public sealed class DevOpsAgentInput
{
    public required string FeatureSlug { get; init; }
    public required string ProjectConstitution { get; init; }
    public required string DeploymentTarget { get; init; }
    public required string RepositorySlug { get; init; }
}
