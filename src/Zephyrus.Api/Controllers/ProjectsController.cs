using Microsoft.AspNetCore.Mvc;
using Zephyrus.Application.UseCases.Projects;
using Zephyrus.Core.Interfaces.Repositories;
using Zephyrus.Core.Entities;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Zephyrus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly DeleteProjectUseCase _deleteProjectUseCase;

    public ProjectsController(IProjectRepository projectRepository, DeleteProjectUseCase deleteProjectUseCase)
    {
        _projectRepository = projectRepository;
        _deleteProjectUseCase = deleteProjectUseCase;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Project>>> GetAll()
    {
        var projects = await _projectRepository.GetAllAsync();
        return Ok(projects);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Project>> GetById(Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            return NotFound();
        }
        return Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<Project>> Create([FromBody] CreateProjectRequest request)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            GitHubRepository = request.GitHubRepository,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _projectRepository.AddAsync(project);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var deleted = await _deleteProjectUseCase.ExecuteAsync(id);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }
}

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GitHubRepository { get; set; } = string.Empty;
}