using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Persistence.Repositories;

public class ModelChoiceRepository : IModelChoiceRepository
{
    private readonly ZephyrusDbContext _db;

    public ModelChoiceRepository(ZephyrusDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(ModelChoiceChange change, CancellationToken ct = default)
    {
        await _db.ModelChoiceChanges.AddAsync(change, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ModelChoiceChange>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        await _db.ModelChoiceChanges
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.ChangedAt)
            .ToListAsync(ct);
}
