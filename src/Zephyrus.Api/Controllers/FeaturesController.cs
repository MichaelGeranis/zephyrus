using Microsoft.AspNetCore.Mvc;
using Zephyrus.Application.Managers;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeaturesController : ControllerBase
{
    private readonly FeatureManager _featureManager;

    public FeaturesController(FeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFeatureRequest request, CancellationToken ct)
    {
        var feature = await _featureManager.CreateAsync(request.ProjectId, request.Prompt, ct);

        return CreatedAtAction(nameof(GetById), new { id = feature.Id }, new FeatureResponse(feature));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var feature = await _featureManager.GetByIdAsync(id, ct);
        if (feature is null)
            return NotFound();

        return Ok(new FeatureResponse(feature));
    }

    [HttpGet("by-project/{projectId:guid}")]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
    {
        var features = await _featureManager.GetByProjectAsync(projectId, ct);
        return Ok(features.Select(f => new FeatureResponse(f)));
    }

    [HttpGet("{id:guid}/artifacts")]
    public async Task<IActionResult> GetArtifacts(
        Guid id,
        [FromServices] IArtifactRepository artifactRepository,
        CancellationToken ct)
    {
        var feature = await _featureManager.GetByIdAsync(id, ct);
        if (feature is null)
            return NotFound();

        var artifacts = await artifactRepository.GetByFeatureIdAsync(id, ct);
        return Ok(artifacts.Select(a => new ArtifactResponse(a)));
    }

    [HttpGet("{id:guid}/artifacts/{artifactId:guid}/content")]
    public async Task<IActionResult> GetArtifactContent(
        Guid id,
        Guid artifactId,
        [FromServices] IArtifactRepository artifactRepository,
        [FromServices] ICodeHost codeHost,
        [FromServices] IProjectRepository projectRepository,
        CancellationToken ct)
    {
        var feature = await _featureManager.GetByIdAsync(id, ct);
        if (feature is null)
            return NotFound();

        var artifact = await artifactRepository.GetByIdAsync(artifactId, ct);
        if (artifact is null || artifact.FeatureId != id)
            return NotFound();

        var project = await projectRepository.GetByIdAsync(feature.ProjectId, ct);
        if (project is null)
            return NotFound();

        var content = await codeHost.GetFileContentAsync(
            project.RepositorySlug, "main", artifact.RepositoryPath, ct);

        if (content is null)
            return NotFound();

        return Ok(new { content });
    }

    [HttpGet("{id:guid}/tasks")]
    public async Task<IActionResult> GetTasks(
        Guid id,
        [FromServices] ITaskItemRepository taskItemRepository,
        CancellationToken ct)
    {
        var feature = await _featureManager.GetByIdAsync(id, ct);
        if (feature is null)
            return NotFound();

        var tasks = await taskItemRepository.GetByFeatureIdAsync(id, ct);
        return Ok(tasks.Select(t => new TaskItemResponse(t)));
    }

    [HttpGet("{id:guid}/pipeline-events")]
    public async Task<IActionResult> GetPipelineEvents(
        Guid id,
        [FromServices] IPipelineEventRepository pipelineEventRepository,
        CancellationToken ct)
    {
        var feature = await _featureManager.GetByIdAsync(id, ct);
        if (feature is null)
            return NotFound();

        var events = await pipelineEventRepository.GetByFeatureIdAsync(id, ct);
        return Ok(events.Select(e => new PipelineEventResponse(e)));
    }

    [HttpPost("{id:guid}/generate-prd")]
    public async Task<IActionResult> GeneratePrd(
        Guid id,
        [FromServices] InvokePrdAgentUseCase useCase,
        CancellationToken ct)
    {
        var artifact = await useCase.ExecuteAsync(id, ct);

        return Ok(new ArtifactResponse(artifact));
    }

    [HttpPost("{id:guid}/artifacts/{artifactId:guid}/approve")]
    public async Task<IActionResult> ApproveArtifact(
        Guid id,
        Guid artifactId,
        [FromBody] ApproveArtifactRequest request,
        [FromServices] ApproveArtifactUseCase useCase,
        CancellationToken ct)
    {
        var artifact = await useCase.ExecuteAsync(id, artifactId, request.ApprovedBy, ct);

        return Ok(new ArtifactResponse(artifact));
    }
}

public record CreateFeatureRequest(Guid ProjectId, string Prompt);

public record ApproveArtifactRequest(string ApprovedBy);

public record FeatureResponse(Guid Id, Guid ProjectId, string Prompt, string Status, DateTime CreatedAt)
{
    public FeatureResponse(Feature f) : this(f.Id, f.ProjectId, f.Prompt, f.Status.ToString(), f.CreatedAt) { }
}

public record ArtifactResponse(
    Guid Id,
    Guid FeatureId,
    string Type,
    string RepositoryPath,
    string? ApprovedBy,
    DateTime? ApprovedAt)
{
    public ArtifactResponse(Artifact a)
        : this(a.Id, a.FeatureId, a.Type.ToString(), a.RepositoryPath, a.ApprovedBy, a.ApprovedAt) { }
}

public record PipelineEventResponse(
    Guid Id,
    Guid FeatureId,
    string FromStatus,
    string ToStatus,
    string TriggeredBy,
    DateTime Timestamp)
{
    public PipelineEventResponse(PipelineEvent e)
        : this(e.Id, e.FeatureId, e.FromStatus.ToString(), e.ToStatus.ToString(), e.TriggeredBy, e.Timestamp) { }
}

public record TaskItemResponse(
    Guid Id,
    Guid FeatureId,
    string Title,
    string Status,
    string AgentType,
    int? ExternalIssueId,
    int? PrId)
{
    public TaskItemResponse(TaskItem t)
        : this(t.Id, t.FeatureId, t.Title, t.Status.ToString(), t.AgentType.ToString(), t.ExternalIssueId, t.PrId) { }
}
