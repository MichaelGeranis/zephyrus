using Microsoft.AspNetCore.Mvc;
using Zephyrus.Application.Managers;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Api.Controllers;

/// <summary>
/// The models each pipeline stage of a project uses. Anyone on the team can
/// see them; only a Tech Lead can change them.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/models")]
public class ProjectModelsController : ControllerBase
{
    private readonly ModelChoiceManager _models;

    public ProjectModelsController(ModelChoiceManager models)
    {
        _models = models;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct) =>
        Ok(new ProjectModelsResponse(await _models.GetAsync(projectId, ct)));

    [HttpPut("{stage}")]
    public async Task<IActionResult> Choose(Guid projectId, AgentJobKind stage, [FromBody] ChooseModelsRequest request, CancellationToken ct) =>
        Ok(new ProjectModelsResponse(await _models.ChooseAsync(projectId, stage, new ModelChoice(request.Primary, request.Fallback), ct)));

    /// <summary>Returns the stage to the configured default.</summary>
    [HttpDelete("{stage}")]
    public async Task<IActionResult> Reset(Guid projectId, AgentJobKind stage, CancellationToken ct) =>
        Ok(new ProjectModelsResponse(await _models.ResetAsync(projectId, stage, ct)));
}

public record ChooseModelsRequest(string Primary, string? Fallback);

public record StageModelsResponse(string Stage, string Primary, string? Fallback, bool IsProjectChoice, string? ChangedBy, DateTime? ChangedAt)
{
    public StageModelsResponse(StageModels s)
        : this(s.Stage.ToString(), s.Models.Primary, s.Models.Fallback, s.IsProjectChoice, s.ChangedBy, s.ChangedAt) { }
}

public record ProjectModelsResponse(IEnumerable<StageModelsResponse> Stages, bool CanChange, IReadOnlyList<string> KnownModels)
{
    public ProjectModelsResponse(ProjectModels m)
        : this(m.Stages.Select(s => new StageModelsResponse(s)), m.CanChange, m.KnownModels) { }
}
