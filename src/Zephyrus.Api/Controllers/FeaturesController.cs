using Microsoft.AspNetCore.Mvc;
using Zephyrus.Application.Managers;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;

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
