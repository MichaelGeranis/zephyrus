using System.Text.Json;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Demo;

/// <summary>
/// A code host backed by a local folder, used in demo mode instead of GitHub.
/// Files are written to <c>{root}/{owner}/{repo}/{branch}/{path}</c> so they
/// survive restarts and can be opened to see what each agent produced.
/// Issues and pull requests are stored as JSON next to the branches.
/// </summary>
public sealed class FileSystemCodeHost : ICodeHost
{
    private const string MainBranch = "main";
    private const string MetadataFolder = ".zephyrus";

    // One process serves every request, so a single lock keeps issue and pull
    // request numbers unique without a database.
    private static readonly SemaphoreSlim Lock = new(1, 1);

    private readonly string _root;

    public FileSystemCodeHost(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public Task<string> CreateBranchAsync(string repo, string branchName, string baseBranch, CancellationToken ct = default)
    {
        Directory.CreateDirectory(BranchPath(repo, branchName));
        return Task.FromResult($"demo-{branchName}");
    }

    public async Task CommitFileAsync(
        string repo, string branch, string path, string content, string commitMessage, CancellationToken ct = default)
    {
        var filePath = FilePath(repo, branch, path);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, content, ct);
    }

    public async Task<string?> GetFileContentAsync(string repo, string branch, string path, CancellationToken ct = default)
    {
        // A branch starts as a copy of main on a real code host; reading
        // through to main gives the same result without copying files.
        foreach (var candidate in new[] { branch, MainBranch }.Distinct())
        {
            var filePath = FilePath(repo, candidate, path);
            if (File.Exists(filePath))
                return await File.ReadAllTextAsync(filePath, ct);
        }

        return null;
    }

    public Task<int> CreatePullRequestAsync(
        string repo, string head, string baseBranch, string title, string body, CancellationToken ct = default)
        => AppendRecordAsync(repo, "pulls", new { head, baseBranch, title, body }, ct);

    public Task<int> CreateIssueAsync(string repo, string title, string body, string[] labels, CancellationToken ct = default)
        => AppendRecordAsync(repo, "issues", new { title, body, labels }, ct);

    public async Task<(string Title, string Body)> GetIssueContentAsync(string repo, int issueNumber, CancellationToken ct = default)
    {
        var filePath = Path.Combine(MetadataPath(repo, "issues"), $"{issueNumber}.json");
        if (!File.Exists(filePath))
            return ($"Issue #{issueNumber}", string.Empty);

        await using var stream = File.OpenRead(filePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = document.RootElement;
        return (root.GetProperty("title").GetString() ?? string.Empty, root.GetProperty("body").GetString() ?? string.Empty);
    }

    private async Task<int> AppendRecordAsync(string repo, string kind, object record, CancellationToken ct)
    {
        await Lock.WaitAsync(ct);
        try
        {
            var folder = MetadataPath(repo, kind);
            Directory.CreateDirectory(folder);

            var number = Directory.GetFiles(folder, "*.json").Length + 1;
            await File.WriteAllTextAsync(
                Path.Combine(folder, $"{number}.json"),
                JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }),
                ct);
            return number;
        }
        finally
        {
            Lock.Release();
        }
    }

    private string RepoPath(string repo) => Contained(Path.Combine(_root, SafeSegments(repo)));

    private string BranchPath(string repo, string branch) =>
        Contained(Path.Combine(RepoPath(repo), SafeSegments(branch)));

    private string FilePath(string repo, string branch, string path) =>
        Contained(Path.Combine(BranchPath(repo, branch), SafeSegments(path)));

    private string MetadataPath(string repo, string kind) =>
        Contained(Path.Combine(RepoPath(repo), MetadataFolder, kind));

    /// <summary>
    /// Repository slugs, branch names and file paths are used as folder names.
    /// Rejects anything that could step outside the root.
    /// </summary>
    private static string SafeSegments(string value)
    {
        var segments = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s is "." or ".." || s == MetadataFolder))
            throw new ArgumentException($"'{value}' is not a valid repository path.", nameof(value));

        return Path.Combine(segments);
    }

    private string Contained(string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"'{path}' is outside the demo repository folder.");

        return full;
    }
}
