using Zephyrus.Core.Entities;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Repository for TaskItem persistence.
/// </summary>
public interface ITaskItemRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default);

    /// <summary>
    /// The task whose pull request is <paramref name="prId"/> within
    /// <paramref name="projectId"/>. Pull request numbers are unique per
    /// repository, so the project scopes the lookup.
    /// </summary>
    Task<TaskItem?> GetByPullRequestAsync(Guid projectId, int prId, CancellationToken ct = default);
    Task AddAsync(TaskItem task, CancellationToken ct = default);
    Task UpdateAsync(TaskItem task, CancellationToken ct = default);
    Task DeleteByFeatureIdAsync(Guid featureId, CancellationToken ct = default);
}
