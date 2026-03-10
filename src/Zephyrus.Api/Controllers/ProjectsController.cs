using Microsoft.AspNetCore.Mvc;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;

namespace Zephyrus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateProjectRequest request,
        [FromServices] CreateProjectUseCase useCase,
        CancellationToken ct)
    {
        var project = await useCase.ExecuteAsync(
            request.Name, request.Description, request.Config, request.RepositorySlug, ct);

        return CreatedAtAction(nameof(GetById), new { id = project.Id }, new ProjectResponse(project));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] GetProjectByIdUseCase useCase,
        CancellationToken ct)
    {
        var project = await useCase.ExecuteAsync(id, ct);
        if (project is null)
            return NotFound();

        return Ok(new ProjectResponse(project));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromServices] GetAllProjectsUseCase useCase,
        CancellationToken ct)
    {
        var projects = await useCase.ExecuteAsync(ct);
        return Ok(projects.Select(p => new ProjectResponse(p)));
    }
}

public record CreateProjectRequest(string Name, string Description, string Config, string RepositorySlug);

public record ProjectResponse(Guid Id, string Name, string Description, string RepositorySlug, DateTime CreatedAt)
{
    public ProjectResponse(Project p) : this(p.Id, p.Name, p.Description, p.RepositorySlug, p.CreatedAt) { }
}
