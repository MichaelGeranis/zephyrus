using Zephyrus.Application.Managers;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;

namespace Zephyrus.UnitTests.Managers;

public class FeatureManagerTests
{
    private readonly InMemoryFeatureRepository _featureRepo = new();
    private readonly InMemoryProjectRepository _projectRepo = new();
    private readonly InMemoryArtifactRepository _artifactRepo = new();
    private readonly FeatureManager _sut;

    private readonly Project _project;

    public FeatureManagerTests()
    {
        _sut = new FeatureManager(_featureRepo, _projectRepo, _artifactRepo);
        _project = Project.Create("TestProject", "desc", "cfg", "org/test", "token");
        _projectRepo.AddAsync(_project).Wait();
    }

    [Fact]
    public async Task CreateAsync_WhenProjectExists_CreatesFeatureInIdeation()
    {
        var feature = await _sut.CreateAsync(_project.Id, "Add dashboard");

        Assert.NotEqual(Guid.Empty, feature.Id);
        Assert.Equal("Add dashboard", feature.Prompt);
        Assert.Equal(FeatureStatus.Ideation, feature.Status);
        Assert.Equal(_project.Id, feature.ProjectId);
    }

    [Fact]
    public async Task CreateAsync_WhenProjectNotFound_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateAsync(Guid.NewGuid(), "Some prompt"));
    }

    [Fact]
    public async Task GetByIdAsync_WhenFeatureExists_ReturnsFeature()
    {
        var created = await _sut.CreateAsync(_project.Id, "Find me");

        var result = await _sut.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal("Find me", result.Prompt);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFeatureDoesNotExist_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsOnlyFeaturesForProject()
    {
        var otherProject = Project.Create("Other", "d", "c", "org/other", "t");
        await _projectRepo.AddAsync(otherProject);

        await _sut.CreateAsync(_project.Id, "Feature A");
        await _sut.CreateAsync(_project.Id, "Feature B");
        await _sut.CreateAsync(otherProject.Id, "Feature C");

        var result = await _sut.GetByProjectAsync(_project.Id);

        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.Equal(_project.Id, f.ProjectId));
    }

    [Fact]
    public async Task GetDeletionPreviewAsync_WithNoArtifacts_ReturnsZeroChildCount()
    {
        var feature = await _sut.CreateAsync(_project.Id, "Standalone feature");

        var preview = await _sut.GetDeletionPreviewAsync(feature.Id);

        Assert.Equal("Standalone feature", preview.EntityTitle);
        Assert.Equal(0, preview.ChildrenCount);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task GetDeletionPreviewAsync_WithArtifacts_ReturnsWarning()
    {
        var feature = await _sut.CreateAsync(_project.Id, "Feature with artifacts");
        await _artifactRepo.AddAsync(Artifact.Create(feature.Id, ArtifactType.Prd));
        await _artifactRepo.AddAsync(Artifact.Create(feature.Id, ArtifactType.Adr));

        var preview = await _sut.GetDeletionPreviewAsync(feature.Id);

        Assert.Equal(2, preview.ChildrenCount);
        Assert.Single(preview.Warnings);
        Assert.Contains("2 artifact(s)", preview.Warnings.First());
    }

    [Fact]
    public async Task GetDeletionPreviewAsync_WhenFeatureNotFound_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetDeletionPreviewAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_WhenFeatureExists_DeletesFeatureAndReturnsCount()
    {
        var feature = await _sut.CreateAsync(_project.Id, "To delete");
        await _artifactRepo.AddAsync(Artifact.Create(feature.Id, ArtifactType.Prd));

        var deletedCount = await _sut.DeleteAsync(feature.Id);

        Assert.Equal(2, deletedCount); // feature + 1 artifact
        var afterDelete = await _featureRepo.GetByIdAsync(feature.Id);
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task DeleteAsync_WhenFeatureNotFound_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.DeleteAsync(Guid.NewGuid()));
    }
}
