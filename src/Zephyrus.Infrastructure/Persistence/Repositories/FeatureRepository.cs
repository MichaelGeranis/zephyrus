using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IFeatureRepository"/>.
/// </summary>
public class FeatureRepository : IFeatureRepository
{
    private readonly ZephyrusDbContext _db;

    public FeatureRepository(ZephyrusDbContext db)
    {
        _db = db;
    }

    public async Task<Feature?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Features
            .Include(f => f.Artifacts)
            .Include(f => f.Tasks)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<Feature?> GetByIdWithArtifactsAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Features
            .Include(f => f.Artifacts)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<IReadOnlyList<Feature>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        return await _db.Features
            .Where(f => f.ProjectId == projectId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Feature feature, CancellationToken ct = default)
    {
        await _db.Features.AddAsync(feature, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Feature feature, CancellationToken ct = default)
    {
        _db.Features.Update(feature);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Feature feature, CancellationToken ct = default)
    {
        _db.Features.Remove(feature);
        await _db.SaveChangesAsync(ct);
    }
}