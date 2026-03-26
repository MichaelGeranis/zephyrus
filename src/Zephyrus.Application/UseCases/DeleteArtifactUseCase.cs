using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Entities;

namespace Zephyrus.Application.UseCases;

public class DeleteArtifactUseCase
{
    private readonly IArtifactRepository _artifactRepository;

    public DeleteArtifactUseCase(IArtifactRepository artifactRepository)
    {
        _artifactRepository = artifactRepository;
    }

    public async Task<bool> ExecuteAsync(Guid id)
    {
        var artifact = await _artifactRepository.GetByIdAsync(id);
        if (artifact == null)
        {
            return false;
        }

        await _artifactRepository.DeleteAsync(artifact);
        return true;
    }
}