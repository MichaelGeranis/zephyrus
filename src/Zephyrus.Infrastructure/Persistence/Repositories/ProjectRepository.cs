using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IProjectRepository"/>.
/// </summary>
public class ProjectRepository : IProjectRepository
{
    private readonly ZephyrusDbContext _db;

    public ProjectRepository(ZephyrusDbContext db)
    {
        _db = db;
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Projects.ToListAsync(ct);
    }

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        await _db.Projects.AddAsync(project, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        _db.Projects.Update(project);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Project project, CancellationToken ct = default)
    {
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);
    }
}
