using Microsoft.AspNetCore.Mvc;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;

    public ProjectsController(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var project = Project.Create(request.Name, request.Description, request.Config, request.RepositorySlug);
        await _projectRepository.AddAsync(project, ct);

        return CreatedAtAction(nameof(GetById), new { id = project.Id }, new ProjectResponse(project));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var project = await _projectRepository.GetByIdAsync(id, ct);
        if (project is null)
            return NotFound();

        return Ok(new ProjectResponse(project));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var projects = await _projectRepository.GetAllAsync(ct);
        return Ok(projects.Select(p => new ProjectResponse(p)));
    }
}

public record CreateProjectRequest(string Name, string Description, string Config, string RepositorySlug);

public record ProjectResponse(Guid Id, string Name, string Description, string RepositorySlug, DateTime CreatedAt)
{
    public ProjectResponse(Project p) : this(p.Id, p.Name, p.Description, p.RepositorySlug, p.CreatedAt) { }
}
