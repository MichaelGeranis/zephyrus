using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.Managers;

public sealed class ProjectManager
{
    private readonly IProjectRepository _projectRepository;

    public ProjectManager(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Project> CreateAsync(string name, string description, string config, string repositorySlug, CancellationToken ct = default)
    {
        var project = Project.Create(name, description, config, repositorySlug);
        await _projectRepository.AddAsync(project, ct);

        return project;
    }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _projectRepository.GetByIdAsync(id, ct);
    }

    public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
    {
        return _projectRepository.GetAllAsync(ct);
    }
}
