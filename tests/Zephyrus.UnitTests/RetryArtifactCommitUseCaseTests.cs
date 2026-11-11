using Zephyrus.Application.Exceptions;
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
            () => _sut.ExecuteAsync(featureId, artifactId, Ct));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArtifactDoesNotBelongToFeature_ShouldThrowRequestRejectedException()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project, Ct);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature, Ct);

        var otherFeature = Feature.Create(project.Id, "other prompt");
        await _featureRepo.AddAsync(otherFeature, Ct);

        var artifact = Artifact.Create(otherFeature.Id, ArtifactType.Prd);
        artifact.SetPendingContent("# PRD content");
        await _artifactRepo.AddAsync(artifact, Ct);

        await Assert.ThrowsAsync<RequestRejectedException>(
            () => _sut.ExecuteAsync(feature.Id, artifact.Id, Ct));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyCommitted_ShouldThrowRequestRejectedException()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project, Ct);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature, Ct);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        artifact.SetPendingContent("# PRD content");
        artifact.MarkCommitSucceeded();
        await _artifactRepo.AddAsync(artifact, Ct);

        await Assert.ThrowsAsync<RequestRejectedException>(
            () => _sut.ExecuteAsync(feature.Id, artifact.Id, Ct));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPendingContent_ShouldThrowRequestRejectedException()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project, Ct);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature, Ct);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact, Ct);

        await Assert.ThrowsAsync<RequestRejectedException>(
            () => _sut.ExecuteAsync(feature.Id, artifact.Id, Ct));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommitSucceeds_ShouldMarkCommitSucceeded()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project, Ct);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature, Ct);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        artifact.SetPendingContent("# PRD content");
        await _artifactRepo.AddAsync(artifact, Ct);

        var result = await _sut.ExecuteAsync(feature.Id, artifact.Id, Ct);

        Assert.True(result.CommitSucceeded);
        Assert.Null(result.PendingContent);
        Assert.True(_codeHost.Files.ContainsKey(("owner/repo", "main", artifact.RepositoryPath)));
        Assert.Equal("# PRD content", _codeHost.Files[("owner/repo", "main", artifact.RepositoryPath)]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGitHubFails_ShouldPropagateException()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project, Ct);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature, Ct);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        artifact.SetPendingContent("# PRD content");
        await _artifactRepo.AddAsync(artifact, Ct);

        var failingCodeHost = new FailingCodeHost();
        var failingFactory = new FakeCodeHostFactory(failingCodeHost);
        var sut = new RetryArtifactCommitUseCase(
            _artifactRepo, _featureRepo, _projectRepo, failingFactory, _invocationRepo);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(feature.Id, artifact.Id, Ct));

        // Artifact should still have pending content (not cleared)
        var updated = await _artifactRepo.GetByIdAsync(artifact.Id, Ct);
        Assert.False(updated!.CommitSucceeded);
        Assert.NotNull(updated.PendingContent);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPendingContentButInvocationExists_ShouldFallbackToInvocationResponse()
    {
        var project = Project.Create("test", "desc", "config", "owner/repo", "token");
        await _projectRepo.AddAsync(project, Ct);

        var feature = Feature.Create(project.Id, "test prompt");
        await _featureRepo.AddAsync(feature, Ct);

        // Artifact without PendingContent (pre-existing record)
        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact, Ct);

        // But there IS an agent invocation with the response
        var invocation = AgentInvocation.Create(feature.Id, "prd", "system", "user", "# Fallback PRD", 100);
        await _invocationRepo.AddAsync(invocation, Ct);

        var result = await _sut.ExecuteAsync(feature.Id, artifact.Id, Ct);

        Assert.True(result.CommitSucceeded);
        Assert.True(_codeHost.Files.ContainsKey(("owner/repo", "main", artifact.RepositoryPath)));
        Assert.Equal("# Fallback PRD", _codeHost.Files[("owner/repo", "main", artifact.RepositoryPath)]);
    }
}
