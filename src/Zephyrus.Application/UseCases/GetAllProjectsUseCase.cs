using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

public sealed class GetAllProjectsUseCase
{
    private readonly IProjectRepository _projectRepository;

    public GetAllProjectsUseCase(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public Task<IReadOnlyList<Project>> ExecuteAsync(CancellationToken ct = default)
    {
        return _projectRepository.GetAllAsync(ct);
    }
}
