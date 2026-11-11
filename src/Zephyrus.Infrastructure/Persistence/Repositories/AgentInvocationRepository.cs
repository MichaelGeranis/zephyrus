using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.AI;

namespace Zephyrus.Infrastructure.Persistence.Repositories;

public class AgentInvocationRepository : IAgentInvocationRepository
{
    private readonly ZephyrusDbContext _db;
    private readonly LanguageModelUsageTracker? _usage;

    public AgentInvocationRepository(ZephyrusDbContext db, LanguageModelUsageTracker? usage = null)
    {
        _db = db;
        _usage = usage;
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
        // The model calls this run made were recorded in this scope; see LanguageModelUsageTracker.
        var usage = _usage?.Drain();
        if (usage is not null)
            invocation.RecordUsage(usage.Model, usage.InputTokens, usage.OutputTokens, usage.EstimatedCostUsd);

        await _db.AgentInvocations.AddAsync(invocation, ct);
        await _db.SaveChangesAsync(ct);
    }
}
