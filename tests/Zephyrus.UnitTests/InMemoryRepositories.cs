using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.UnitTests;

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

    public Task DeleteAsync(Artifact artifact, CancellationToken ct = default)
    {
        _artifacts.RemoveAll(a => a.Id == artifact.Id);
        return Task.CompletedTask;
    }
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

    public Task DeleteAsync(Feature feature, CancellationToken ct = default)
    {
        _features.RemoveAll(f => f.Id == feature.Id);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryProjectRepository : IProjectRepository
{
    private readonly List<Project> _projects = new();

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_projects.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Project>>(_projects.ToList());

    public Task<Project?> GetByRepositorySlugAsync(string repositorySlug, CancellationToken ct = default)
        => Task.FromResult(_projects.FirstOrDefault(p => p.RepositorySlug == repositorySlug));

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        _projects.Add(project);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Project project, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeleteAsync(Project project, CancellationToken ct = default)
    {
        _projects.RemoveAll(p => p.Id == project.Id);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryPipelineEventRepository : IPipelineEventRepository
{
    private readonly List<PipelineEvent> _events = new();

    public Task<IReadOnlyList<PipelineEvent>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PipelineEvent>>(_events.Where(e => e.FeatureId == featureId).ToList());

    public Task AddAsync(PipelineEvent pipelineEvent, CancellationToken ct = default)
    {
        _events.Add(pipelineEvent);
        return Task.CompletedTask;
    }

    public IReadOnlyList<PipelineEvent> All => _events.AsReadOnly();
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
    public List<string> CreatedBranches { get; } = new();

    private int _nextPrNumber = 1;
    private int _nextIssueNumber = 1;

    public Task<string> CreateBranchAsync(string repo, string branchName, string baseBranch, CancellationToken ct = default)
    {
        CreatedBranches.Add(branchName);
        return Task.FromResult("sha-" + branchName);
    }

    public Task CommitFileAsync(string repo, string branch, string path, string content, string commitMessage, CancellationToken ct = default)
    {
        Files[(repo, branch, path)] = content;
        return Task.CompletedTask;
    }

    public Task<int> CreatePullRequestAsync(string repo, string head, string baseBranch, string title, string body, CancellationToken ct = default)
        => Task.FromResult(_nextPrNumber++);

    public Task<int> CreateIssueAsync(string repo, string title, string body, string[] labels, CancellationToken ct = default)
        => Task.FromResult(_nextIssueNumber++);

    public Task<string?> GetFileContentAsync(string repo, string branch, string path, CancellationToken ct = default)
    {
        Files.TryGetValue((repo, branch, path), out var content);
        return Task.FromResult(content);
    }

    public Task<(string Title, string Body)> GetIssueContentAsync(string repo, int issueNumber, CancellationToken ct = default)
        => Task.FromResult(($"Issue #{issueNumber}", $"Body for issue #{issueNumber}"));
}

internal sealed class FakeCodeHostFactory : ICodeHostFactory
{
    private readonly ICodeHost _codeHost;
    public FakeCodeHostFactory(ICodeHost codeHost) { _codeHost = codeHost; }
    public ICodeHost Create(string token) => _codeHost;
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

    public Task<(string Title, string Body)> GetIssueContentAsync(string repo, int issueNumber, CancellationToken ct = default)
        => throw new InvalidOperationException("GitHub commit failed");
}
