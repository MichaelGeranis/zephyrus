using Microsoft.AspNetCore.Mvc;
using Zephyrus.Application.Managers;
using Zephyrus.Core.Entities;

namespace Zephyrus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ProjectManager _projectManager;

    public ProjectsController(ProjectManager projectManager)
    {
        _projectManager = projectManager;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var project = await _projectManager.CreateAsync(
            request.Name, request.Description, request.Config, request.RepositorySlug, request.GitHubToken, ct);

        return CreatedAtAction(nameof(GetById), new { id = project.Id }, new ProjectResponse(project));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var project = await _projectManager.GetByIdAsync(id, ct);
        if (project is null)
            return NotFound();

        return Ok(new ProjectResponse(project));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var projects = await _projectManager.GetAllAsync(ct);
        return Ok(projects.Select(p => new ProjectResponse(p)));
    }
}

public record CreateProjectRequest(string Name, string Description, string Config, string RepositorySlug, string GitHubToken);

public record ProjectResponse(Guid Id, string Name, string Description, string RepositorySlug, DateTime CreatedAt)
{
    public ProjectResponse(Project p) : this(p.Id, p.Name, p.Description, p.RepositorySlug, p.CreatedAt) { }
}
