using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Entities;

/// <summary>
/// A unit of work moving through the delivery pipeline.
/// Status transitions are governed by <see cref="Pipeline.PipelineStateMachine"/>.
/// </summary>
public class Feature
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }

    /// <summary>
    /// The original idea prompt written by the PM+EM.
    /// </summary>
    public string Prompt { get; private set; } = string.Empty;

    public FeatureStatus Status { get; internal set; }
    public DateTime CreatedAt { get; private set; }

    public Project Project { get; private set; } = null!;
    public IReadOnlyCollection<Artifact> Artifacts => _artifacts.AsReadOnly();
    public IReadOnlyCollection<TaskItem> Tasks => _tasks.AsReadOnly();
    public IReadOnlyCollection<PipelineEvent> PipelineEvents => _pipelineEvents.AsReadOnly();
    public IReadOnlyCollection<Deployment> Deployments => _deployments.AsReadOnly();

    private readonly List<Artifact> _artifacts = new();
    private readonly List<TaskItem> _tasks = new();
    private readonly List<PipelineEvent> _pipelineEvents = new();
    private readonly List<Deployment> _deployments = new();

    private Feature() { }

    public static Feature Create(Guid projectId, string prompt)
    {
        return new Feature
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Prompt = prompt,
            Status = FeatureStatus.Ideation,
            CreatedAt = DateTime.UtcNow
        };
    }
}
