using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Persistence.Repositories;

public class AgentInvocationRepository : IAgentInvocationRepository
{
    private readonly ZephyrusDbContext _db;

    public AgentInvocationRepository(ZephyrusDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AgentInvocation>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
    {
        return await _db.AgentInvocations
            .Where(i => i.FeatureId == featureId)
            .OrderBy(i => i.InvokedAt)
            .ToListAsync(ct);
    }

    public async Task<AgentInvocation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.AgentInvocations.FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task AddAsync(AgentInvocation invocation, CancellationToken ct = default)
    {
        await _db.AgentInvocations.AddAsync(invocation, ct);
        await _db.SaveChangesAsync(ct);
    }
}
