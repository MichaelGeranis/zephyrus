using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

public sealed class GetProjectByIdUseCase
{
    private readonly IProjectRepository _projectRepository;

    public GetProjectByIdUseCase(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public Task<Project?> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        return _projectRepository.GetByIdAsync(id, ct);
    }
}
