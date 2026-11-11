using Octokit;
using Octokit.Internal;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.GitHub;

/// <summary>
/// Octokit.net implementation of <see cref="ICodeHost"/> for GitHub. Each call
/// authenticates for the repository it names: with a GitHub App installation
/// token when the App is installed there, otherwise with the project's token.
/// </summary>
public sealed class GitHubCodeHost : ICodeHost
{
    private readonly string _projectToken;
    private readonly GitHubAppTokenSource? _app;
    private readonly Func<HttpMessageHandler>? _handler;

    public GitHubCodeHost(string projectToken, GitHubAppTokenSource? app = null)
        : this(projectToken, app, handler: null)
    {
    }

    /// <summary>Tests pass a handler that plays GitHub, so no request leaves the process.</summary>
    internal GitHubCodeHost(string projectToken, GitHubAppTokenSource? app, Func<HttpMessageHandler>? handler)
    {
        _projectToken = projectToken;
        _app = app;
        _handler = handler;
    }

    private async Task<GitHubClient> ClientForAsync(string owner, string repoName, CancellationToken ct)
    {
        var credentials = new InMemoryCredentialStore(new Credentials(await TokenForAsync(owner, repoName, ct)));
        var product = new ProductHeaderValue("Zephyrus");
        if (_handler is null)
            return new GitHubClient(product, credentials);

        return new GitHubClient(new Connection(
            product, GitHubClient.GitHubApiUrl, credentials, new HttpClientAdapter(_handler), new SimpleJsonSerializer()));
    }

    /// <summary>The App's installation token for the repository if it has one, else the project's token.</summary>
    internal async Task<string> TokenForAsync(string owner, string repoName, CancellationToken ct)
    {
        var token = (_app is null ? null : await _app.TokenForAsync(owner, repoName, ct)) ?? _projectToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                $"No GitHub credentials for {owner}/{repoName}: install the Zephyrus GitHub App on it, or set the project's token.");
        }

        return token;
    }

    /// <inheritdoc />
    public async Task<string> CreateBranchAsync(
        string repo, string branchName, string baseBranch, CancellationToken ct = default)
    {
        var (owner, repoName) = ParseRepo(repo);
        var client = await ClientForAsync(owner, repoName, ct);

        // If the branch already exists (e.g. rerun), return its current SHA
        try
        {
            var existing = await client.Git.Reference.Get(owner, repoName, $"heads/{branchName}");
            return existing.Object.Sha;
        }
        catch (NotFoundException)
        {
            // Branch does not exist yet — create it
        }

        var baseBranchRef = await client.Git.Reference.Get(owner, repoName, $"heads/{baseBranch}");
        var baseSha = baseBranchRef.Object.Sha;

        var newRef = await client.Git.Reference.Create(owner, repoName,
            new NewReference($"refs/heads/{branchName}", baseSha));

        return newRef.Object.Sha;
    }

    /// <inheritdoc />
    public async Task CommitFileAsync(
        string repo, string branch, string path, string content, string commitMessage, CancellationToken ct = default)
    {
        var (owner, repoName) = ParseRepo(repo);
        var client = await ClientForAsync(owner, repoName, ct);

        // Try to get existing file to obtain its SHA (for updates)
        string? existingSha = null;
        try
        {
            var existingContents = await client.Repository.Content.GetAllContentsByRef(owner, repoName, path, branch);
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
            await client.Repository.Content.UpdateFile(owner, repoName, path,
                new UpdateFileRequest(commitMessage, content, existingSha, branch));
        }
        else
        {
            await client.Repository.Content.CreateFile(owner, repoName, path,
                new CreateFileRequest(commitMessage, content, branch));
        }
    }

    /// <inheritdoc />
    public async Task<int> CreatePullRequestAsync(
        string repo, string head, string baseBranch, string title, string body, CancellationToken ct = default)
    {
        var (owner, repoName) = ParseRepo(repo);
        var client = await ClientForAsync(owner, repoName, ct);

        var pr = await client.PullRequest.Create(owner, repoName,
            new NewPullRequest(title, head, baseBranch) { Body = body });

        return pr.Number;
    }

    /// <inheritdoc />
    public async Task<int> CreateIssueAsync(
        string repo, string title, string body, string[] labels, CancellationToken ct = default)
    {
        var (owner, repoName) = ParseRepo(repo);
        var client = await ClientForAsync(owner, repoName, ct);

        var newIssue = new NewIssue(title) { Body = body };
        foreach (var label in labels)
        {
            newIssue.Labels.Add(label);
        }

        var issue = await client.Issue.Create(owner, repoName, newIssue);

        return issue.Number;
    }

    /// <inheritdoc />
    public async Task<string?> GetFileContentAsync(
        string repo, string branch, string path, CancellationToken ct = default)
    {
        var (owner, repoName) = ParseRepo(repo);
        var client = await ClientForAsync(owner, repoName, ct);

        try
        {
            var contents = await client.Repository.Content.GetAllContentsByRef(owner, repoName, path, branch);
            return contents.Count > 0 ? contents[0].Content : null;
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<(string Title, string Body)> GetIssueContentAsync(
        string repo, int issueNumber, CancellationToken ct = default)
    {
        var (owner, repoName) = ParseRepo(repo);
        var client = await ClientForAsync(owner, repoName, ct);
        var issue = await client.Issue.Get(owner, repoName, issueNumber);
        return (issue.Title, issue.Body ?? string.Empty);
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
