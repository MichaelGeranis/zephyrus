using Microsoft.AspNetCore.Mvc;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;
using Zephyrus.Infrastructure.Repositories;

namespace Zephyrus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtifactsController : ControllerBase
{
    private readonly IArtifactRepository _artifactRepository;
    private readonly DeleteArtifactUseCase _deleteArtifactUseCase;

    public ArtifactsController(
        IArtifactRepository artifactRepository,
        DeleteArtifactUseCase deleteArtifactUseCase)
    {
        _artifactRepository = artifactRepository;
        _deleteArtifactUseCase = deleteArtifactUseCase;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Artifact>>> GetAll()
    {
        var artifacts = await _artifactRepository.GetAllAsync();
        return Ok(artifacts);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Artifact>> GetById(int id)
    {
        var artifact = await _artifactRepository.GetByIdAsync(id);
        if (artifact == null)
        {
            return NotFound();
        }
        return Ok(artifact);
    }

    [HttpPost]
    public async Task<ActionResult<Artifact>> Create(Artifact artifact)
    {
        var created = await _artifactRepository.CreateAsync(artifact);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, Artifact artifact)
    {
        if (id != artifact.Id)
        {
            return BadRequest();
        }

        var existing = await _artifactRepository.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        await _artifactRepository.UpdateAsync(artifact);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            await _deleteArtifactUseCase.ExecuteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception)
        {
            return Conflict();
        }
    }
}