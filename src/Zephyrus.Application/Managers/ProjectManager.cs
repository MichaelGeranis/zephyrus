using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.Managers;

public sealed class ProjectManager
{
    private readonly IProjectRepository _projectRepository;
    private readonly IFeatureRepository _featureRepository;

    public ProjectManager(IProjectRepository projectRepository, IFeatureRepository featureRepository)
    {
        _projectRepository = projectRepository;
        _featureRepository = featureRepository;
    }

    public async Task<Project> CreateAsync(string name, string description, string config, string repositorySlug, string gitHubToken, CancellationToken ct = default)
    {
        var project = Project.Create(name, description, config, repositorySlug, gitHubToken);
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

    public async Task<DeletionPreview> GetDeletionPreviewAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepository.GetByIdAsync(id, ct)
            ?? throw new ArgumentException($"Project '{id}' not found.");

        var features = await _featureRepository.GetByProjectIdAsync(id, ct);
        var featureCount = features.Count;

        var warnings = featureCount > 0
            ? new[] { $"This will permanently delete {featureCount} feature(s) and all associated artifacts, tasks, and events." }
            : Array.Empty<string>();

        return new DeletionPreview(project.Name, featureCount, warnings);
    }

    public async Task<int> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepository.GetByIdAsync(id, ct)
            ?? throw new ArgumentException($"Project '{id}' not found.");

        var features = await _featureRepository.GetByProjectIdAsync(id, ct);
        var childCount = features.Count;

        await _projectRepository.DeleteAsync(project, ct);

        return 1 + childCount;
    }
}

public record DeletionPreview(string EntityTitle, int ChildrenCount, IEnumerable<string> Warnings);
