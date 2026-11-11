namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Abstraction over code host operations (GitHub, GitLab, Bitbucket, etc.).
/// </summary>
public interface ICodeHost
{
    Task<string> CreateBranchAsync(string repo, string branchName, string baseBranch, CancellationToken ct = default);
    Task CommitFileAsync(string repo, string branch, string path, string content, string commitMessage, CancellationToken ct = default);
    Task<int> CreatePullRequestAsync(string repo, string head, string baseBranch, string title, string body, CancellationToken ct = default);
    Task<int> CreateIssueAsync(string repo, string title, string body, string[] labels, CancellationToken ct = default);
    Task<string?> GetFileContentAsync(string repo, string branch, string path, CancellationToken ct = default);
    Task<(string Title, string Body)> GetIssueContentAsync(string repo, int issueNumber, CancellationToken ct = default);
}
