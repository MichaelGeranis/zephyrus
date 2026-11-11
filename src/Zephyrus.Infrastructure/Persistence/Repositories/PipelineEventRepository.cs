using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPipelineEventRepository"/>.
/// </summary>
public class PipelineEventRepository : IPipelineEventRepository
{
    private readonly ZephyrusDbContext _db;

    public PipelineEventRepository(ZephyrusDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PipelineEvent>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
    {
        return await _db.PipelineEvents
            .Where(pe => pe.FeatureId == featureId)
            .OrderBy(pe => pe.Timestamp)
            .ToListAsync(ct);
    }

    public async Task AddAsync(PipelineEvent pipelineEvent, CancellationToken ct = default)
    {
        await _db.PipelineEvents.AddAsync(pipelineEvent, ct);
        await _db.SaveChangesAsync(ct);
    }
}
