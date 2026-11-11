using Zephyrus.Core.Entities;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Repository for Project aggregate persistence.
/// </summary>
public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// The project whose repository is <paramref name="repositorySlug"/> ("owner/repo").
    /// Used to route incoming code-host webhooks to a project.
    /// </summary>
    Task<Project?> GetByRepositorySlugAsync(string repositorySlug, CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
    Task DeleteAsync(Project project, CancellationToken ct = default);
}
