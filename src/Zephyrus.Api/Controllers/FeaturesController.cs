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

    [HttpGet("{id:guid}/agent-invocations")]
    public async Task<IActionResult> GetAgentInvocations(
        Guid id,
        [FromServices] IAgentInvocationRepository agentInvocationRepository,
        CancellationToken ct)
    {
        var feature = await _featureManager.GetByIdAsync(id, ct);
        if (feature is null)
            return NotFound();

        var invocations = await agentInvocationRepository.GetByFeatureIdAsync(id, ct);
        return Ok(invocations.Select(i => new AgentInvocationSummaryResponse(i)));
    }

    [HttpGet("{id:guid}/agent-invocations/{invocationId:guid}")]
    public async Task<IActionResult> GetAgentInvocationDetail(
        Guid id,
        Guid invocationId,
        [FromServices] IAgentInvocationRepository agentInvocationRepository,
        CancellationToken ct)
    {
        var invocation = await agentInvocationRepository.GetByIdAsync(invocationId, ct);
        if (invocation is null || invocation.FeatureId != id)
            return NotFound();

        return Ok(new AgentInvocationDetailResponse(invocation));
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
    DateTime? ApprovedAt,
    bool CommitSucceeded)
{
    public ArtifactResponse(Artifact a)
        : this(a.Id, a.FeatureId, a.Type.ToString(), a.RepositoryPath, a.ApprovedBy, a.ApprovedAt, a.CommitSucceeded) { }
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

public record AgentInvocationSummaryResponse(
    Guid Id,
    Guid FeatureId,
    string AgentName,
    DateTime InvokedAt,
    int DurationMs)
{
    public AgentInvocationSummaryResponse(AgentInvocation i)
        : this(i.Id, i.FeatureId, i.AgentName, i.InvokedAt, i.DurationMs) { }
}

public record AgentInvocationDetailResponse(
    Guid Id,
    Guid FeatureId,
    string AgentName,
    string SystemPrompt,
    string UserMessage,
    string Response,
    DateTime InvokedAt,
    int DurationMs)
{
    public AgentInvocationDetailResponse(AgentInvocation i)
        : this(i.Id, i.FeatureId, i.AgentName, i.SystemPrompt, i.UserMessage, i.Response, i.InvokedAt, i.DurationMs) { }
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
