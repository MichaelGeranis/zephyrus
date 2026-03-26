using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IArtifactRepository"/>.
/// </summary>
public class ArtifactRepository : IArtifactRepository
{
    private readonly ZephyrusDbContext _db;

    public ArtifactRepository(ZephyrusDbContext db)
    {
        _db = db;
    }

    public async Task<Artifact?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Artifacts.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<IReadOnlyList<Artifact>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
    {
        return await _db.Artifacts
            .Where(a => a.FeatureId == featureId)
            .ToListAsync(ct);
    }

    public async Task<Artifact?> GetByFeatureIdAndTypeAsync(Guid featureId, ArtifactType type, CancellationToken ct = default)
    {
        return await _db.Artifacts
            .FirstOrDefaultAsync(a => a.FeatureId == featureId && a.Type == type, ct);
    }

    public async Task AddAsync(Artifact artifact, CancellationToken ct = default)
    {
        await _db.Artifacts.AddAsync(artifact, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Artifact artifact, CancellationToken ct = default)
    {
        _db.Artifacts.Update(artifact);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Artifact artifact, CancellationToken ct = default)
    {
        _db.Artifacts.Remove(artifact);
        await _db.SaveChangesAsync(ct);
    }
}
