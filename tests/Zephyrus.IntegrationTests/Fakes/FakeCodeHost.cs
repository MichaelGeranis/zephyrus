using Zephyrus.Core.Interfaces;

namespace Zephyrus.IntegrationTests.Fakes;

/// <summary>
/// In-memory code host that stores files in a dictionary.
/// Tracks all operations for test assertions.
/// </summary>
public sealed class FakeCodeHost : ICodeHost
{
    /// <summary>Files stored as (repo, branch, path) → content.</summary>
    public Dictionary<(string Repo, string Branch, string Path), string> Files { get; } = new();

    public List<string> CreatedBranches { get; } = new();
    public List<(string Repo, int Number)> CreatedPrs { get; } = new();
    public List<(string Repo, int Number)> CreatedIssues { get; } = new();

    private int _nextPrNumber = 1;
    private int _nextIssueNumber = 1;

    public Task<string> CreateBranchAsync(string repo, string branchName, string baseBranch, CancellationToken ct = default)
    {
        CreatedBranches.Add(branchName);
        return Task.FromResult("fake-sha-" + branchName);
    }

    public Task CommitFileAsync(string repo, string branch, string path, string content, string commitMessage, CancellationToken ct = default)
    {
        Files[(repo, branch, path)] = content;
        return Task.CompletedTask;
    }

    public Task<int> CreatePullRequestAsync(string repo, string head, string baseBranch, string title, string body, CancellationToken ct = default)
    {
        var number = _nextPrNumber++;
        CreatedPrs.Add((repo, number));
        return Task.FromResult(number);
    }

    public Task<int> CreateIssueAsync(string repo, string title, string body, string[] labels, CancellationToken ct = default)
    {
        var number = _nextIssueNumber++;
        CreatedIssues.Add((repo, number));
        return Task.FromResult(number);
    }

    public Task<string?> GetFileContentAsync(string repo, string branch, string path, CancellationToken ct = default)
    {
        Files.TryGetValue((repo, branch, path), out var content);
        return Task.FromResult(content);
    }
}
