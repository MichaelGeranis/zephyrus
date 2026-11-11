using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Entities;

/// <summary>
/// Tracks a deployment of a Feature to an environment.
/// </summary>
public class Deployment
{
    public Guid Id { get; private set; }
    public Guid FeatureId { get; private set; }
    public string Sha { get; private set; } = string.Empty;
    public string Environment { get; private set; } = string.Empty;
    public DateTime DeployedAt { get; private set; }
    public DeploymentStatus Status { get; private set; }

    public Feature Feature { get; private set; } = null!;

    private Deployment() { }

    public static Deployment Create(Guid featureId, string sha, string environment)
    {
        return new Deployment
        {
            Id = Guid.NewGuid(),
            FeatureId = featureId,
            Sha = sha,
            Environment = environment,
            DeployedAt = DateTime.UtcNow,
            Status = DeploymentStatus.Pending
        };
    }

    /// <summary>
    /// Marks deployment as successful.
    /// </summary>
    public void MarkSuccess()
    {
        Status = DeploymentStatus.Success;
    }

    /// <summary>
    /// Marks deployment as failed.
    /// </summary>
    public void MarkFailed()
    {
        Status = DeploymentStatus.Failed;
    }
}
