using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAgentJobRepository"/>.
/// </summary>
/// <remarks>
/// Claiming uses optimistic concurrency on <see cref="AgentJobRecord.ConcurrencyStamp"/>
/// rather than database row locks, so it behaves the same on PostgreSQL and on
/// the SQLite database the tests use. When another worker wins a row, this
/// worker moves on to the next candidate.
/// </remarks>
public class AgentJobRepository : IAgentJobRepository
{
    private const int CandidatesPerClaim = 10;

    private readonly ZephyrusDbContext _db;

    public AgentJobRepository(ZephyrusDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(AgentJobRecord job, CancellationToken ct = default)
    {
        await _db.AgentJobs.AddAsync(job, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AgentJobRecord?> TryClaimNextAsync(DateTime now, TimeSpan lease, CancellationToken ct = default)
    {
        var candidates = await _db.AgentJobs
            .Where(j => (j.Status == AgentJobStatus.Queued && j.NextAttemptAt <= now)
                        || (j.Status == AgentJobStatus.Running && j.LeaseExpiresAt < now))
            .OrderBy(j => j.EnqueuedAt)
            .Take(CandidatesPerClaim)
            .ToListAsync(ct);

        foreach (var job in candidates)
        {
            var claimed = job.TryClaim(now, lease);

            try
            {
                // Saved either way: a job whose lease expired with no attempts
                // left was just marked failed and must be stored as such.
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another worker changed this job first. Forget our copy.
                _db.Entry(job).State = EntityState.Detached;
                continue;
            }

            if (claimed)
                return job;
        }

        return null;
    }

    public async Task UpdateAsync(AgentJobRecord job, CancellationToken ct = default)
    {
        _db.AgentJobs.Update(job);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AgentJobRecord>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
    {
        return await _db.AgentJobs
            .Where(j => j.FeatureId == featureId)
            .OrderBy(j => j.EnqueuedAt)
            .ToListAsync(ct);
    }
}
