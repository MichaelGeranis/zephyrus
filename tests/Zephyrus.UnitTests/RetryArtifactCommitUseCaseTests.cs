using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Exceptions;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.UnitTests;

public class RetryArtifactCommitUseCaseTests
{
    private readonly InMemoryArtifactRepository _artifactRepo = new();
    private readonly InMemoryFeatureRepository _featureRepo = new();
    private readonly InMemoryProjectRepository _projectRepo = new();
    private readonly InMemoryAgentInvocationRepository _invocationRepo = new();
    private readonly FakeCodeHost _codeHost = new();
    private readonly FakeCodeHostFactory _codeHostFactory;
    private readonly RetryArtifactCommitUseCase _sut;

    public RetryArtifactCommitUseCaseTests()
    {
        _codeHostFactory = new FakeCodeHostFactory(_codeHost);
        _sut = new RetryArtifactCommitUseCase(
            _artifactRepo, _featureRepo, _projectRepo, _codeHostFactory, _invocationRepo);
    }

    [Fact]
    public async Task ExecuteAsync_WhenArtifactNotFound_ShouldThrowArtifactNotFoundException()
    {
        var featureId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArtifactNotFoundException>(
            () => _sut.ExecuteAsync(featureId, artifactId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArtifactDoesNotBelongToFeature_ShouldThrowInvalidOperationException()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature);

        var otherFeature = Feature.Create(project.Id, "other prompt");
        await _featureRepo.AddAsync(otherFeature);

        var artifact = Artifact.Create(otherFeature.Id, ArtifactType.Prd);
        artifact.SetPendingContent("# PRD content");
        await _artifactRepo.AddAsync(artifact);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync(feature.Id, artifact.Id));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyCommitted_ShouldThrowInvalidOperationException()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        artifact.SetPendingContent("# PRD content");
        artifact.MarkCommitSucceeded();
        await _artifactRepo.AddAsync(artifact);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync(feature.Id, artifact.Id));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPendingContent_ShouldThrowInvalidOperationException()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync(feature.Id, artifact.Id));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommitSucceeds_ShouldMarkCommitSucceeded()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        artifact.SetPendingContent("# PRD content");
        await _artifactRepo.AddAsync(artifact);

        var result = await _sut.ExecuteAsync(feature.Id, artifact.Id);

        Assert.True(result.CommitSucceeded);
        Assert.Null(result.PendingContent);
        Assert.True(_codeHost.Files.ContainsKey(("owner/repo", "main", artifact.RepositoryPath)));
        Assert.Equal("# PRD content", _codeHost.Files[("owner/repo", "main", artifact.RepositoryPath)]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGitHubFails_ShouldPropagateException()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        artifact.SetPendingContent("# PRD content");
        await _artifactRepo.AddAsync(artifact);

        var failingCodeHost = new FailingCodeHost();
        var failingFactory = new FakeCodeHostFactory(failingCodeHost);
        var sut = new RetryArtifactCommitUseCase(
            _artifactRepo, _featureRepo, _projectRepo, failingFactory, _invocationRepo);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(feature.Id, artifact.Id));

        // Artifact should still have pending content (not cleared)
        var updated = await _artifactRepo.GetByIdAsync(artifact.Id);
        Assert.False(updated!.CommitSucceeded);
        Assert.NotNull(updated.PendingContent);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPendingContentButInvocationExists_ShouldFallbackToInvocationResponse()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature);

        // Artifact without PendingContent (pre-existing record)
        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        // But there IS an agent invocation with the response
        var invocation = AgentInvocation.Create(feature.Id, "prd", "system", "user", "# Fallback PRD", 100);
        await _invocationRepo.AddAsync(invocation);

        var result = await _sut.ExecuteAsync(feature.Id, artifact.Id);

        Assert.True(result.CommitSucceeded);
        Assert.True(_codeHost.Files.ContainsKey(("owner/repo", "main", artifact.RepositoryPath)));
        Assert.Equal("# Fallback PRD", _codeHost.Files[("owner/repo", "main", artifact.RepositoryPath)]);
    }
}

#region In-memory fakes

internal sealed class InMemoryArtifactRepository : IArtifactRepository
{
    private readonly List<Artifact> _artifacts = new();

    public Task<Artifact?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_artifacts.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Artifact>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Artifact>>(_artifacts.Where(a => a.FeatureId == featureId).ToList());

    public Task<Artifact?> GetByFeatureIdAndTypeAsync(Guid featureId, ArtifactType type, CancellationToken ct = default)
        => Task.FromResult(_artifacts.FirstOrDefault(a => a.FeatureId == featureId && a.Type == type));

    public Task AddAsync(Artifact artifact, CancellationToken ct = default)
    {
        _artifacts.Add(artifact);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Artifact artifact, CancellationToken ct = default)
        => Task.CompletedTask;
}

internal sealed class InMemoryFeatureRepository : IFeatureRepository
{
    private readonly List<Feature> _features = new();

    public Task<Feature?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_features.FirstOrDefault(f => f.Id == id));

    public Task<IReadOnlyList<Feature>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Feature>>(_features.Where(f => f.ProjectId == projectId).ToList());

    public Task AddAsync(Feature feature, CancellationToken ct = default)
    {
        _features.Add(feature);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Feature feature, CancellationToken ct = default)
        => Task.CompletedTask;
}

internal sealed class InMemoryProjectRepository : IProjectRepository
{
    private readonly List<Project> _projects = new();

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_projects.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Project>>(_projects.ToList());

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        _projects.Add(project);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Project project, CancellationToken ct = default)
        => Task.CompletedTask;
}

internal sealed class InMemoryAgentInvocationRepository : IAgentInvocationRepository
{
    private readonly List<AgentInvocation> _invocations = new();

    public Task<IReadOnlyList<AgentInvocation>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentInvocation>>(_invocations.Where(i => i.FeatureId == featureId).ToList());

    public Task<AgentInvocation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_invocations.FirstOrDefault(i => i.Id == id));

    public Task AddAsync(AgentInvocation invocation, CancellationToken ct = default)
    {
        _invocations.Add(invocation);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCodeHost : ICodeHost
{
    public Dictionary<(string Repo, string Branch, string Path), string> Files { get; } = new();

    public Task<string> CreateBranchAsync(string repo, string branchName, string baseBranch, CancellationToken ct = default)
        => Task.FromResult("fake-sha");

    public Task CommitFileAsync(string repo, string branch, string path, string content, string commitMessage, CancellationToken ct = default)
    {
        Files[(repo, branch, path)] = content;
        return Task.CompletedTask;
    }

    public Task<int> CreatePullRequestAsync(string repo, string head, string baseBranch, string title, string body, CancellationToken ct = default)
        => Task.FromResult(1);

    public Task<int> CreateIssueAsync(string repo, string title, string body, string[] labels, CancellationToken ct = default)
        => Task.FromResult(1);

    public Task<string?> GetFileContentAsync(string repo, string branch, string path, CancellationToken ct = default)
    {
        Files.TryGetValue((repo, branch, path), out var content);
        return Task.FromResult(content);
    }
}

internal sealed class FailingCodeHost : ICodeHost
{
    public Task<string> CreateBranchAsync(string repo, string branchName, string baseBranch, CancellationToken ct = default)
        => throw new InvalidOperationException("GitHub commit failed");

    public Task CommitFileAsync(string repo, string branch, string path, string content, string commitMessage, CancellationToken ct = default)
        => throw new InvalidOperationException("GitHub commit failed");

    public Task<int> CreatePullRequestAsync(string repo, string head, string baseBranch, string title, string body, CancellationToken ct = default)
        => throw new InvalidOperationException("GitHub commit failed");

    public Task<int> CreateIssueAsync(string repo, string title, string body, string[] labels, CancellationToken ct = default)
        => throw new InvalidOperationException("GitHub commit failed");

    public Task<string?> GetFileContentAsync(string repo, string branch, string path, CancellationToken ct = default)
        => throw new InvalidOperationException("GitHub commit failed");
}

internal sealed class FakeCodeHostFactory : ICodeHostFactory
{
    private readonly ICodeHost _codeHost;
    public FakeCodeHostFactory(ICodeHost codeHost) => _codeHost = codeHost;
    public ICodeHost Create(string token) => _codeHost;
}

#endregion
