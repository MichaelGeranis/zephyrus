using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Zephyrus.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations at startup. Used by the Docker setup so a
/// fresh database is ready without running <c>dotnet ef</c> by hand.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ZephyrusDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
