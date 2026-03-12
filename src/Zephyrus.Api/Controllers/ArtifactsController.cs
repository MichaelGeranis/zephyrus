using Microsoft.AspNetCore.Mvc;
using Zephyrus.Application.Managers;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;

namespace Zephyrus.Api.Controllers;

[ApiController]
[Route("api/features/{featureId:guid}/artifacts")]
public class ArtifactsController : ControllerBase
{
    private readonly ArtifactManager _artifactManager;

    public ArtifactsController(ArtifactManager artifactManager)
    {
        _artifactManager = artifactManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetByFeature(Guid featureId, CancellationToken ct)
    {
        var artifacts = await _artifactManager.GetByFeatureIdAsync(featureId, ct);
        if (artifacts is null)
            return NotFound();

        return Ok(artifacts.Select(a => new ArtifactResponse(a)));
    }

    [HttpGet("{artifactId:guid}/content")]
    public async Task<IActionResult> GetContent(Guid featureId, Guid artifactId, CancellationToken ct)
    {
        var content = await _artifactManager.GetContentAsync(featureId, artifactId, ct);
        if (content is null)
            return NotFound();

        return Ok(new { content });
    }

    [HttpPost("{artifactId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid featureId,
        Guid artifactId,
        [FromBody] ApproveArtifactRequest request,
        [FromServices] ApproveArtifactUseCase useCase,
        CancellationToken ct)
    {
        var artifact = await useCase.ExecuteAsync(featureId, artifactId, request.ApprovedBy, ct);

        return Ok(new ArtifactResponse(artifact));
    }
}
