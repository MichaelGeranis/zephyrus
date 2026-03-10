using Microsoft.Extensions.Options;
using Octokit;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.GitHub;

/// <summary>
/// Octokit.net implementation of <see cref="ICodeHost"/> for GitHub.
/// </summary>
public sealed class GitHubCodeHost : ICodeHost
{
    private readonly GitHubClient _client;

    public GitHubCodeHost(IOptions<GitHubCodeHostOptions> options)
    {
        _client = new GitHubClient(new ProductHeaderValue("Zephyrus"))
        {
            Credentials = new Credentials(options.Value.Token)
        };
    }

    /// <inheritdoc />
    public async Task<string> CreateBranchAsync(
        string repo, string branchName, string baseBranch, CancellationToken ct = default)
    {
        var (owner, repoName) = ParseRepo(repo);

        // Get the SHA of the base branch
        var baseBranchRef = await _client.Git.Reference.Get(owner, repoName, $"heads/{baseBranch}");
        var baseSha = baseBranchRef.Object.Sha;

        // Create the new branch reference
        var newRef = await _client.Git.Reference.Create(owner, repoName,
            new NewReference($"refs/heads/{branchName}", baseSha));

        return newRef.Object.Sha;
    }

    /// <inheritdoc />
    public async Task CommitFileAsync(
        string repo, string branch, string path, string content, string commitMessage, CancellationToken ct = default)
    {
        var (owner, repoName) = ParseRepo(repo);

        // Try to get existing file to obtain its SHA (for updates)
        string? existingSha = null;
        try
        {
            var existingContents = await _client.Repository.Content.GetAllContentsByRef(owner, repoName, path, branch);
            if (existingContents.Count > 0)
            {
                existingSha = existingContents[0].Sha;
            }
        }
        catch (NotFoundException)
        {
            // File does not exist yet — this is a create, not an update
        }

        if (existingSha is not null)
        {
            await _client.Repository.Content.UpdateFile(owner, repoName, path,
                new UpdateFileRequest(commitMessage, content, existingSha, branch));
        }
        else
        {
            await _client.Repository.Content.CreateFile(owner, repoName, path,
                new CreateFileRequest(commitMessage, content, branch));
        }
    }

    /// <inheritdoc />
    public async Task<int> CreatePullRequestAsync(
        string repo, string head, string baseBranch, string title, string body, CancellationToken ct = default)
    {
        var (owner, repoName) = ParseRepo(repo);

        var pr = await _client.PullRequest.Create(owner, repoName,
            new NewPullRequest(title, head, baseBranch) { Body = body });

        return pr.Number;
    }

    /// <inheritdoc />
    public async Task<int> CreateIssueAsync(
        string repo, string title, string body, string[] labels, CancellationToken ct = default)
    {
        var (owner, repoName) = ParseRepo(repo);

        var newIssue = new NewIssue(title) { Body = body };
        foreach (var label in labels)
        {
            newIssue.Labels.Add(label);
        }

        var issue = await _client.Issue.Create(owner, repoName, newIssue);

        return issue.Number;
    }

    /// <inheritdoc />
    public async Task<string?> GetFileContentAsync(
        string repo, string branch, string path, CancellationToken ct = default)
    {
        var (owner, repoName) = ParseRepo(repo);

        try
        {
            var contents = await _client.Repository.Content.GetAllContentsByRef(owner, repoName, path, branch);
            return contents.Count > 0 ? contents[0].Content : null;
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Splits an "owner/repo" string into its two components.
    /// </summary>
    private static (string Owner, string RepoName) ParseRepo(string repo)
    {
        var parts = repo.Split('/', 2);
        if (parts.Length != 2)
            throw new ArgumentException($"Repository must be in 'owner/repo' format, got: '{repo}'", nameof(repo));

        return (parts[0], parts[1]);
    }
}
