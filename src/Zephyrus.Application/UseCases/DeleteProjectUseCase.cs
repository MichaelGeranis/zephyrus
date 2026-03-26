using Zephyrus.Application.Common.Interfaces;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

public class DeleteProjectUseCase
{
    private readonly IRepository<Project> _projectRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProjectUseCase(IRepository<Project> projectRepository, IUnitOfWork unitOfWork)
    {
        _projectRepository = projectRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> ExecuteAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            return false;
        }

        _projectRepository.Delete(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return true;
    }
}