using Zephyrus.Core.Entities;

namespace Zephyrus.Core.Interfaces;

public interface IModelChoiceRepository
{
    Task AddAsync(ModelChoiceChange change, CancellationToken ct = default);

    /// <summary>Every change for the project, oldest first.</summary>
    Task<IReadOnlyList<ModelChoiceChange>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
}
