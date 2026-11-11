using Microsoft.EntityFrameworkCore;

namespace Zephyrus.Infrastructure.Persistence;

/// <summary>
/// Answers whether the database can be reached, for the API's readiness check.
/// Lives here because only Persistence may touch the DbContext.
/// </summary>
public sealed class DatabaseHealthProbe
{
    private readonly ZephyrusDbContext _db;

    public DatabaseHealthProbe(ZephyrusDbContext db)
    {
        _db = db;
    }

    public Task<bool> CanConnectAsync(CancellationToken ct = default) => _db.Database.CanConnectAsync(ct);
}
