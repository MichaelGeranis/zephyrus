using Microsoft.AspNetCore.Mvc;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;

namespace Zephyrus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeaturesController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateFeatureRequest request,
        [FromServices] CreateFeatureUseCase useCase,
        CancellationToken ct)
    {
        var feature = await useCase.ExecuteAsync(request.ProjectId, request.Prompt, ct);

        return CreatedAtAction(nameof(GetById), new { id = feature.Id }, new FeatureResponse(feature));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] GetFeatureByIdUseCase useCase,
        CancellationToken ct)
    {
        var feature = await useCase.ExecuteAsync(id, ct);
        if (feature is null)
            return NotFound();

        return Ok(new FeatureResponse(feature));
    }

    [HttpGet("by-project/{projectId:guid}")]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromServices] GetFeaturesByProjectUseCase useCase,
        CancellationToken ct)
    {
        var features = await useCase.ExecuteAsync(projectId, ct);
        return Ok(features.Select(f => new FeatureResponse(f)));
    }

    [HttpPost("{id:guid}/generate-prd")]
    public async Task<IActionResult> GeneratePrd(
        Guid id,
        [FromServices] InvokePrdAgentUseCase useCase,
        CancellationToken ct)
    {
        var artifact = await useCase.ExecuteAsync(id, ct);

        return Ok(new
        {
            artifactId = artifact.Id,
            repositoryPath = artifact.RepositoryPath,
            type = artifact.Type.ToString()
        });
    }
}

public record CreateFeatureRequest(Guid ProjectId, string Prompt);

public record FeatureResponse(Guid Id, Guid ProjectId, string Prompt, string Status, DateTime CreatedAt)
{
    public FeatureResponse(Feature f) : this(f.Id, f.ProjectId, f.Prompt, f.Status.ToString(), f.CreatedAt) { }
}
