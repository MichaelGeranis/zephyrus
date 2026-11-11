using Zephyrus.Application.Exceptions;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.Managers;

/// <summary>
/// Exports the audit trail of a feature or a whole project as one document:
/// every pipeline transition and who caused it, every approval, every agent
/// run with its prompts, response, model, prompt version and cost, the jobs,
/// tasks and deployments, and the project's model choices. Artifact contents
/// stay in the repository; the project's code-host token is never exported.
/// </summary>
public sealed class AuditExportManager
{
    public const string Format = "zephyrus.audit/v1";

    private readonly IProjectRepository _projects;
    private readonly IFeatureRepository _features;
    private readonly IPipelineEventRepository _events;
    private readonly IArtifactRepository _artifacts;
    private readonly IAgentInvocationRepository _runs;
    private readonly IAgentJobRepository _jobs;
    private readonly ITaskItemRepository _tasks;
    private readonly IDeploymentRepository _deployments;
    private readonly IModelChoiceRepository _modelChoices;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public AuditExportManager(
        IProjectRepository projects,
        IFeatureRepository features,
        IPipelineEventRepository events,
        IArtifactRepository artifacts,
        IAgentInvocationRepository runs,
        IAgentJobRepository jobs,
        ITaskItemRepository tasks,
        IDeploymentRepository deployments,
        IModelChoiceRepository modelChoices,
        IUserContext user,
        TimeProvider time)
    {
        _projects = projects;
        _features = features;
        _events = events;
        _artifacts = artifacts;
        _runs = runs;
        _jobs = jobs;
        _tasks = tasks;
        _deployments = deployments;
        _modelChoices = modelChoices;
        _user = user;
        _time = time;
    }

    public async Task<AuditExport> ExportFeatureAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _features.GetByIdAsync(featureId, ct)
            ?? throw new ResourceNotFoundException($"Feature '{featureId}' not found.");
        var project = await _projects.GetByIdAsync(feature.ProjectId, ct)
            ?? throw new ResourceNotFoundException($"Project '{feature.ProjectId}' not found.");

        return await ExportAsync(project, new[] { feature }, ct);
    }

    public async Task<AuditExport> ExportProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _projects.GetByIdAsync(projectId, ct)
            ?? throw new ResourceNotFoundException($"Project '{projectId}' not found.");

        var features = await _features.GetByProjectIdAsync(projectId, ct);
        return await ExportAsync(project, features.OrderBy(f => f.CreatedAt), ct);
    }

    private async Task<AuditExport> ExportAsync(Project project, IEnumerable<Feature> features, CancellationToken ct)
    {
        var exported = new List<FeatureAudit>();
        foreach (var feature in features)
            exported.Add(await FeatureAuditAsync(feature, ct));

        var choices = await _modelChoices.GetByProjectIdAsync(project.Id, ct);

        return new AuditExport(
            Format,
            _time.GetUtcNow().UtcDateTime,
            _user.UserId,
            new ProjectAudit(project.Id, project.Name, project.RepositorySlug, project.CreatedAt, project.Config),
            choices.Select(c => new ModelChoiceAudit(
                c.Stage.ToString(), c.Primary, c.Fallback, c.IsReset, c.ChangedBy, c.ChangedAt)).ToList(),
            exported);
    }

    private async Task<FeatureAudit> FeatureAuditAsync(Feature feature, CancellationToken ct)
    {
        var events = await _events.GetByFeatureIdAsync(feature.Id, ct);
        var artifacts = await _artifacts.GetByFeatureIdAsync(feature.Id, ct);
        var runs = await _runs.GetByFeatureIdAsync(feature.Id, ct);
        var jobs = await _jobs.GetByFeatureIdAsync(feature.Id, ct);
        var tasks = await _tasks.GetByFeatureIdAsync(feature.Id, ct);
        var deployments = await _deployments.GetByFeatureIdAsync(feature.Id, ct);

        return new FeatureAudit(
            feature.Id,
            feature.Prompt,
            feature.Status.ToString(),
            feature.CreatedAt,
            events.OrderBy(e => e.Timestamp)
                .Select(e => new PipelineEventAudit(e.FromStatus.ToString(), e.ToStatus.ToString(), e.TriggeredBy, e.Timestamp))
                .ToList(),
            artifacts
                .Select(a => new ArtifactAudit(
                    a.Id, a.Type.ToString(), a.RepositoryPath, a.CommitSucceeded, a.ApprovedBy, a.ApprovedAt,
                    runs.Where(r => r.ArtifactId == a.Id).Select(r => r.Id).ToList()))
                .ToList(),
            runs.OrderBy(r => r.InvokedAt)
                .Select(r => new AgentRunAudit(
                    r.Id, r.AgentName, r.InvokedAt, r.DurationMs, r.Model, r.PromptVersion,
                    r.InputTokens, r.OutputTokens, r.EstimatedCostUsd, r.ArtifactId,
                    r.SystemPrompt, r.UserMessage, r.Response))
                .ToList(),
            jobs.OrderBy(j => j.EnqueuedAt)
                .Select(j => new AgentJobAudit(
                    j.Id, j.Kind.ToString(), j.Status.ToString(), j.ForceRerun, j.Attempts, j.LastError,
                    j.EnqueuedAt, j.StartedAt, j.CompletedAt))
                .ToList(),
            tasks.Select(t => new TaskAudit(t.Id, t.Title, t.AgentType.ToString(), t.Status.ToString(), t.ExternalIssueId, t.PrId))
                .ToList(),
            deployments.OrderBy(d => d.DeployedAt)
                .Select(d => new DeploymentAudit(d.Sha, d.Environment, d.Status.ToString(), d.DeployedAt))
                .ToList());
    }
}

/// <summary>The exported audit trail. One shape for a feature or a whole project.</summary>
public sealed record AuditExport(
    string Format,
    DateTime ExportedAt,
    string? ExportedBy,
    ProjectAudit Project,
    IReadOnlyList<ModelChoiceAudit> ModelChoiceChanges,
    IReadOnlyList<FeatureAudit> Features);

public sealed record ProjectAudit(Guid Id, string Name, string RepositorySlug, DateTime CreatedAt, string Constitution);

public sealed record ModelChoiceAudit(string Stage, string? Primary, string? Fallback, bool IsReset, string ChangedBy, DateTime ChangedAt);

public sealed record FeatureAudit(
    Guid Id,
    string Prompt,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<PipelineEventAudit> PipelineEvents,
    IReadOnlyList<ArtifactAudit> Artifacts,
    IReadOnlyList<AgentRunAudit> AgentRuns,
    IReadOnlyList<AgentJobAudit> AgentJobs,
    IReadOnlyList<TaskAudit> Tasks,
    IReadOnlyList<DeploymentAudit> Deployments);

public sealed record PipelineEventAudit(string From, string To, string TriggeredBy, DateTime At);

/// <param name="ProducedBy">The agent runs that produced this artifact.</param>
public sealed record ArtifactAudit(
    Guid Id, string Type, string RepositoryPath, bool Committed, string? ApprovedBy, DateTime? ApprovedAt,
    IReadOnlyList<Guid> ProducedBy);

/// <param name="ArtifactId">The artifact the run produced; it may since have been replaced by a re-run.</param>
public sealed record AgentRunAudit(
    Guid Id, string Agent, DateTime InvokedAt, int DurationMs, string? Model, string PromptVersion,
    int? InputTokens, int? OutputTokens, decimal? EstimatedCostUsd, Guid? ArtifactId,
    string SystemPrompt, string UserMessage, string Response);

public sealed record AgentJobAudit(
    Guid Id, string Kind, string Status, bool ForceRerun, int Attempts, string? LastError,
    DateTime EnqueuedAt, DateTime? StartedAt, DateTime? CompletedAt);

public sealed record TaskAudit(Guid Id, string Title, string AgentType, string Status, int? IssueNumber, int? PullRequestNumber);

public sealed record DeploymentAudit(string Sha, string Environment, string Status, DateTime DeployedAt);
