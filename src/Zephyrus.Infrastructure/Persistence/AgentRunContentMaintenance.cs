using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zephyrus.Core.Entities;
using Zephyrus.Infrastructure.Security;

namespace Zephyrus.Infrastructure.Persistence;

/// <summary>How long agent runs keep what they read and wrote.</summary>
public sealed class AgentRunContentOptions
{
    public const string SectionName = "AgentRuns";

    /// <summary>
    /// Days after which a run's user message and response are removed; the run's
    /// record (agent, model, prompt version, tokens, cost, artifact) stays. 0 keeps
    /// them for as long as the feature exists.
    /// </summary>
    public int ContentRetentionDays { get; set; }
}

/// <summary>
/// Keeps agent run content protected: encrypts runs stored before their content
/// was encrypted, and removes content older than the retention period.
/// </summary>
/// <remarks>
/// Lives in Persistence because it works on stored rows. It touches runs in
/// batches so a large history never sits in memory at once.
/// </remarks>
public sealed class AgentRunContentMaintenance
{
    private const int BatchSize = 100;

    private readonly ZephyrusDbContext _db;
    private readonly AgentRunContentOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<AgentRunContentMaintenance> _logger;

    public AgentRunContentMaintenance(
        ZephyrusDbContext db,
        IOptions<AgentRunContentOptions> options,
        TimeProvider time,
        ILogger<AgentRunContentMaintenance> logger)
    {
        _db = db;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    /// <summary>Encrypts the runs whose content is still stored as written.</summary>
    /// <returns>How many runs were encrypted</returns>
    public async Task<int> EncryptStoredContentAsync(CancellationToken ct = default)
    {
        // The converter encrypts on write, so a value comparison in LINQ would
        // compare against a fresh ciphertext: find the rows by their stored form.
        var prefix = DataProtectionSecretProtector.Prefix + "%";
        var total = 0;
        while (true)
        {
            var ids = await _db.Database
                .SqlQuery<Guid>($"""
                    SELECT id AS "Value"
                    FROM agent_invocations
                    WHERE (user_message <> '' AND user_message NOT LIKE {prefix})
                      OR (response <> '' AND response NOT LIKE {prefix})
                    LIMIT {BatchSize}
                    """)
                .ToListAsync(ct);
            if (ids.Count == 0)
                break;

            var runs = await _db.AgentInvocations.Where(i => ids.Contains(i.Id)).ToListAsync(ct);
            foreach (var run in runs)
            {
                var entry = _db.Entry(run);
                entry.Property(i => i.UserMessage).IsModified = true;
                entry.Property(i => i.Response).IsModified = true;
            }

            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            total += runs.Count;
        }

        if (total > 0)
            _logger.LogInformation("Encrypted the stored content of {Count} agent run(s)", total);
        return total;
    }

    /// <summary>Removes the content of runs older than the retention period.</summary>
    /// <returns>How many runs had their content removed</returns>
    public async Task<int> RemoveExpiredContentAsync(CancellationToken ct = default)
    {
        if (_options.ContentRetentionDays <= 0)
            return 0;

        var now = _time.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-_options.ContentRetentionDays);
        var total = 0;
        while (true)
        {
            var runs = await _db.AgentInvocations
                .Where(i => i.ContentRemovedAt == null && i.InvokedAt < cutoff)
                .OrderBy(i => i.InvokedAt)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (runs.Count == 0)
                break;

            foreach (var run in runs)
                run.RemoveContent(now);

            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            total += runs.Count;
        }

        if (total > 0)
            _logger.LogInformation(
                "Removed the content of {Count} agent run(s) older than {Days} days", total, _options.ContentRetentionDays);
        return total;
    }
}
