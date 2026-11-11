using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IDeploymentRepository"/>.
/// </summary>
public class DeploymentRepository : IDeploymentRepository
{
    private readonly ZephyrusDbContext _db;

    public DeploymentRepository(ZephyrusDbContext db)
    {
        _db = db;
    }

    public async Task<Deployment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Deployments.FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<Deployment?> GetByShaAsync(string sha, CancellationToken ct = default)
    {
        return await _db.Deployments.FirstOrDefaultAsync(d => d.Sha == sha, ct);
    }

    public async Task<IReadOnlyList<Deployment>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
    {
        return await _db.Deployments
            .Where(d => d.FeatureId == featureId)
            .OrderBy(d => d.DeployedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Deployment deployment, CancellationToken ct = default)
    {
        await _db.Deployments.AddAsync(deployment, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Deployment deployment, CancellationToken ct = default)
    {
        _db.Deployments.Update(deployment);
        await _db.SaveChangesAsync(ct);
    }
}
