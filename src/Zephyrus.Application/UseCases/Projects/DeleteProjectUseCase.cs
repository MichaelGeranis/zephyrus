using System;
using System.Threading.Tasks;
using Zephyrus.Core.Interfaces.Repositories;

namespace Zephyrus.Application.UseCases.Projects;

public class DeleteProjectUseCase
{
    private readonly IProjectRepository _projectRepository;

    public DeleteProjectUseCase(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<bool> ExecuteAsync(Guid projectId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            return false;
        }

        await _projectRepository.DeleteAsync(project);
        return true;
    }
}