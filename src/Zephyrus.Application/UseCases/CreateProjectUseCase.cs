using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

public sealed class CreateProjectUseCase
{
    private readonly IProjectRepository _projectRepository;

    public CreateProjectUseCase(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Project> ExecuteAsync(string name, string description, string config, string repositorySlug, CancellationToken ct = default)
    {
        var project = Project.Create(name, description, config, repositorySlug);
        await _projectRepository.AddAsync(project, ct);

        return project;
    }
}
