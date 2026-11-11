using Zephyrus.Application.Exceptions;
using Zephyrus.Application.Managers;
using Zephyrus.Core.Entities;

namespace Zephyrus.UnitTests.Managers;

public class ProjectManagerTests
{
    private readonly InMemoryProjectRepository _projectRepo = new();
    private readonly InMemoryFeatureRepository _featureRepo = new();
    private readonly ProjectManager _sut;

    public ProjectManagerTests()
    {
        _sut = new ProjectManager(_projectRepo, _featureRepo);
    }

    [Fact]
    public async Task CreateAsync_WhenCalled_CreatesAndPersistsProject()
    {
        var project = await _sut.CreateAsync("MyApp", "A test app", "config: test", "org/repo", "token", Ct);

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("MyApp", project.Name);
        Assert.Equal("org/repo", project.RepositorySlug);

        var persisted = await _projectRepo.GetByIdAsync(project.Id, Ct);
        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task GetByIdAsync_WhenProjectExists_ReturnsProject()
    {
        var created = await _sut.CreateAsync("Alpha", "desc", "cfg", "org/alpha", "tok", Ct);

        var result = await _sut.GetByIdAsync(created.Id, Ct);

        Assert.NotNull(result);
        Assert.Equal("Alpha", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WhenProjectDoesNotExist_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_WhenCalled_ReturnsAllProjects()
    {
        await _sut.CreateAsync("P1", "d", "c", "o/p1", "t", Ct);
        await _sut.CreateAsync("P2", "d", "c", "o/p2", "t", Ct);

        var all = await _sut.GetAllAsync(Ct);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetDeletionPreviewAsync_WithNoFeatures_ReturnsZeroChildCount()
    {
        var project = await _sut.CreateAsync("Standalone", "d", "c", "o/s", "t", Ct);

        var preview = await _sut.GetDeletionPreviewAsync(project.Id, Ct);

        Assert.Equal("Standalone", preview.EntityTitle);
        Assert.Equal(0, preview.ChildrenCount);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public async Task GetDeletionPreviewAsync_WithFeatures_ReturnsWarning()
    {
        var project = await _sut.CreateAsync("WithFeatures", "d", "c", "o/wf", "t", Ct);
        await _featureRepo.AddAsync(Feature.Create(project.Id, "Feature 1"), Ct);
        await _featureRepo.AddAsync(Feature.Create(project.Id, "Feature 2"), Ct);

        var preview = await _sut.GetDeletionPreviewAsync(project.Id, Ct);

        Assert.Equal(2, preview.ChildrenCount);
        Assert.Single(preview.Warnings);
        Assert.Contains("2 feature(s)", preview.Warnings.First());
    }

    [Fact]
    public async Task GetDeletionPreviewAsync_WhenProjectNotFound_ThrowsResourceNotFoundException()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _sut.GetDeletionPreviewAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task DeleteAsync_WhenProjectExists_DeletesProjectAndReturnsCount()
    {
        var project = await _sut.CreateAsync("ToDelete", "d", "c", "o/td", "t", Ct);
        await _featureRepo.AddAsync(Feature.Create(project.Id, "Child feature"), Ct);

        var deletedCount = await _sut.DeleteAsync(project.Id, Ct);

        Assert.Equal(2, deletedCount); // project + 1 feature
        var afterDelete = await _projectRepo.GetByIdAsync(project.Id, Ct);
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task DeleteAsync_WhenProjectNotFound_ThrowsResourceNotFoundException()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _sut.DeleteAsync(Guid.NewGuid(), Ct));
    }
}
