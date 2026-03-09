namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Abstraction over GitHub operations. Implemented in Infrastructure via Octokit.net.
/// </summary>
public interface ICodeHost
{
    Task<string> CreateBranchAsync(string repo, string branchName, string baseBranch, CancellationToken ct = default);
    Task CommitFileAsync(string repo, string branch, string path, string content, string commitMessage, CancellationToken ct = default);
    Task<int> CreatePullRequestAsync(string repo, string head, string baseBranch, string title, string body, CancellationToken ct = default);
    Task<int> CreateIssueAsync(string repo, string title, string body, string[] labels, CancellationToken ct = default);
}
