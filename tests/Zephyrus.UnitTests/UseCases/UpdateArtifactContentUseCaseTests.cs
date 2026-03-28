using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Exceptions;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.UnitTests.UseCases;

public class UpdateArtifactContentUseCaseTests
{
    private readonly InMemoryArtifactRepository _artifactRepo = new();
    private readonly InMemoryFeatureRepository _featureRepo = new();
    private readonly InMemoryProjectRepository _projectRepo = new();
    private readonly FakeCodeHost _codeHost = new();
    private readonly FakeCodeHostFactory _codeHostFactory;
    private readonly UpdateArtifactContentUseCase _sut;

    private readonly Project _project;
    private readonly Feature _feature;

    public UpdateArtifactContentUseCaseTests()
    {
        _codeHostFactory = new FakeCodeHostFactory(_codeHost);
        _sut = new UpdateArtifactContentUseCase(_artifactRepo, _featureRepo, _projectRepo, _codeHostFactory);

        _project = Project.Create("TestProject", "desc", "cfg", "org/repo", "token");
        _projectRepo.AddAsync(_project).Wait();

        _feature = Feature.Create(_project.Id, "my feature");
        _feature.Advance(); // Ideation → PrdPending
        _featureRepo.AddAsync(_feature).Wait();
    }

    [Fact]
    public async Task ExecuteAsync_WhenArtifactNotFound_ThrowsArtifactNotFoundException()
    {
        await Assert.ThrowsAsync<ArtifactNotFoundException>(() =>
            _sut.ExecuteAsync(_feature.Id, Guid.NewGuid(), "new content"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArtifactBelongsToDifferentFeature_ThrowsInvalidOperationException()
    {
        var artifact = Artifact.Create(Guid.NewGuid(), ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(_feature.Id, artifact.Id, "new content"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArtifactAlreadyApproved_ThrowsInvalidOperationException()
    {
        var artifact = Artifact.Create(_feature.Id, ArtifactType.Prd);
        artifact.Approve("someone");
        await _artifactRepo.AddAsync(artifact);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(_feature.Id, artifact.Id, "updated content"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenFeatureNotFound_ThrowsInvalidOperationException()
    {
        var artifact = Artifact.Create(_feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        // Remove feature so it can't be found
        await _featureRepo.DeleteAsync(_feature);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(_feature.Id, artifact.Id, "content"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_CommitsContentToCodeHost()
    {
        var artifact = Artifact.Create(_feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        await _sut.ExecuteAsync(_feature.Id, artifact.Id, "# Updated PRD Content");

        Assert.True(_codeHost.Files.ContainsKey(("org/repo", "main", artifact.RepositoryPath)));
        Assert.Equal("# Updated PRD Content", _codeHost.Files[("org/repo", "main", artifact.RepositoryPath)]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_ReturnsArtifact()
    {
        var artifact = Artifact.Create(_feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        var result = await _sut.ExecuteAsync(_feature.Id, artifact.Id, "some content");

        Assert.Equal(artifact.Id, result.Id);
    }
}
