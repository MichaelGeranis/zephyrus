using Microsoft.AspNetCore.Mvc;
using Zephyrus.Application.UseCases.Features;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces.Repositories;

namespace Zephyrus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeaturesController : ControllerBase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly DeleteFeatureUseCase _deleteFeatureUseCase;

    public FeaturesController(
        IFeatureRepository featureRepository,
        DeleteFeatureUseCase deleteFeatureUseCase)
    {
        _featureRepository = featureRepository;
        _deleteFeatureUseCase = deleteFeatureUseCase;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Feature>> GetById(Guid id)
    {
        var feature = await _featureRepository.GetByIdAsync(id);
        if (feature == null)
        {
            return NotFound();
        }

        return Ok(feature);
    }

    [HttpGet("project/{projectId}")]
    public async Task<ActionResult<IEnumerable<Feature>>> GetByProjectId(Guid projectId)
    {
        var features = await _featureRepository.GetByProjectIdAsync(projectId);
        return Ok(features);
    }

    [HttpPost]
    public async Task<ActionResult<Feature>> Create([FromBody] CreateFeatureRequest request)
    {
        var feature = new Feature
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            Name = request.Name,
            Description = request.Description,
            Status = FeatureStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _featureRepository.CreateAsync(feature);
        return CreatedAtAction(nameof(GetById), new { id = feature.Id }, feature);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _deleteFeatureUseCase.ExecuteAsync(id);
        
        return result switch
        {
            DeleteFeatureResult.Success => NoContent(),
            DeleteFeatureResult.NotFound => NotFound(),
            DeleteFeatureResult.Conflict => Conflict(),
            _ => throw new InvalidOperationException($"Unexpected delete result: {result}")
        };
    }
}

public class CreateFeatureRequest
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}