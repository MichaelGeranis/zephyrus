using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITaskItemRepository"/>.
/// </summary>
public class TaskItemRepository : ITaskItemRepository
{
    private readonly ZephyrusDbContext _db;

    public TaskItemRepository(ZephyrusDbContext db)
    {
        _db = db;
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<TaskItem>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
    {
        return await _db.TaskItems
            .Where(t => t.FeatureId == featureId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TaskItem task, CancellationToken ct = default)
    {
        await _db.TaskItems.AddAsync(task, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken ct = default)
    {
        _db.TaskItems.Update(task);
        await _db.SaveChangesAsync(ct);
    }
}
