using Zephyrus.Application.Managers;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;

namespace Zephyrus.UnitTests.Managers;

public class ArtifactManagerTests
{
    private readonly InMemoryArtifactRepository _artifactRepo = new();
    private readonly InMemoryFeatureRepository _featureRepo = new();
    private readonly InMemoryProjectRepository _projectRepo = new();
    private readonly FakeCodeHost _codeHost = new();
    private readonly FakeCodeHostFactory _codeHostFactory;
    private readonly ArtifactManager _sut;

    private readonly Project _project;
    private readonly Feature _feature;

    public ArtifactManagerTests()
    {
        _codeHostFactory = new FakeCodeHostFactory(_codeHost);
        _sut = new ArtifactManager(_artifactRepo, _featureRepo, _projectRepo, _codeHostFactory);

        _project = Project.Create("TestProject", "desc", "cfg", "org/test", "token");
        _projectRepo.AddAsync(_project).Wait();

        _feature = Feature.Create(_project.Id, "Test feature");
        _featureRepo.AddAsync(_feature).Wait();
    }

    [Fact]
    public async Task GetByFeatureIdAsync_WhenFeatureExists_ReturnsArtifacts()
    {
        var artifact = Artifact.Create(_feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        var result = await _sut.GetByFeatureIdAsync(_feature.Id);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(ArtifactType.Prd, result[0].Type);
    }

    [Fact]
    public async Task GetByFeatureIdAsync_WhenFeatureDoesNotExist_ReturnsNull()
    {
        var result = await _sut.GetByFeatureIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenArtifactExists_ReturnsArtifact()
    {
        var artifact = Artifact.Create(_feature.Id, ArtifactType.Adr);
        await _artifactRepo.AddAsync(artifact);

        var result = await _sut.GetByIdAsync(artifact.Id);

        Assert.NotNull(result);
        Assert.Equal(ArtifactType.Adr, result.Type);
    }

    [Fact]
    public async Task GetByIdAsync_WhenArtifactDoesNotExist_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetContentAsync_WhenArtifactExists_ReturnsFileContent()
    {
        var artifact = Artifact.Create(_feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);
        _codeHost.Files[("org/test", "main", artifact.RepositoryPath)] = "# PRD Content";

        var content = await _sut.GetContentAsync(_feature.Id, artifact.Id);

        Assert.Equal("# PRD Content", content);
    }

    [Fact]
    public async Task GetContentAsync_WhenFeatureDoesNotExist_ReturnsNull()
    {
        var artifact = Artifact.Create(_feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        var content = await _sut.GetContentAsync(Guid.NewGuid(), artifact.Id);

        Assert.Null(content);
    }

    [Fact]
    public async Task GetContentAsync_WhenArtifactDoesNotExist_ReturnsNull()
    {
        var content = await _sut.GetContentAsync(_feature.Id, Guid.NewGuid());

        Assert.Null(content);
    }

    [Fact]
    public async Task GetContentAsync_WhenArtifactBelongsToDifferentFeature_ReturnsNull()
    {
        var otherFeature = Feature.Create(_project.Id, "Other");
        await _featureRepo.AddAsync(otherFeature);
        var artifact = Artifact.Create(otherFeature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        var content = await _sut.GetContentAsync(_feature.Id, artifact.Id);

        Assert.Null(content);
    }

    [Fact]
    public async Task GetDeletionPreviewAsync_WhenArtifactExists_ReturnsPreview()
    {
        var artifact = Artifact.Create(_feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        var preview = await _sut.GetDeletionPreviewAsync(_feature.Id, artifact.Id);

        Assert.Equal("Prd", preview.EntityTitle);
        Assert.Equal(0, preview.ChildrenCount);
    }

    [Fact]
    public async Task GetDeletionPreviewAsync_WhenArtifactNotFound_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetDeletionPreviewAsync(_feature.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetDeletionPreviewAsync_WhenArtifactBelongsToDifferentFeature_ThrowsArgumentException()
    {
        var artifact = Artifact.Create(Guid.NewGuid(), ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetDeletionPreviewAsync(_feature.Id, artifact.Id));
    }

    [Fact]
    public async Task DeleteAsync_WhenArtifactExists_DeletesArtifact()
    {
        var artifact = Artifact.Create(_feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        await _sut.DeleteAsync(_feature.Id, artifact.Id);

        var afterDelete = await _artifactRepo.GetByIdAsync(artifact.Id);
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task DeleteAsync_WhenArtifactNotFound_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.DeleteAsync(_feature.Id, Guid.NewGuid()));
    }
}
