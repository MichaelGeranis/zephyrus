using System.Net;
using System.Text;
using Zephyrus.Infrastructure.GitHub;

namespace Zephyrus.UnitTests.GitHub;

/// <summary>
/// The GitHub code host against an in-memory GitHub: the requests Octokit
/// sends and how the answers are read. No request leaves the process.
/// </summary>
public class GitHubCodeHostTests
{
    private const string Repo = "acme/billing";
    private readonly FakeGitHub _github = new();

    private GitHubCodeHost CodeHost(string token = "ghp_project") => new(token, app: null, () => _github);

    [Fact]
    public async Task CreateBranchAsync_WhenBranchDoesNotExist_ShouldCreateItFromTheBaseBranch()
    {
        _github
            .On(HttpMethod.Get, "/repos/acme/billing/git/refs/heads/main", FakeGitHub.Reference("refs/heads/main", "base-sha"))
            .On(HttpMethod.Post, "/repos/acme/billing/git/refs", FakeGitHub.Reference("refs/heads/feature/x", "base-sha"),
                HttpStatusCode.Created);

        var sha = await CodeHost().CreateBranchAsync(Repo, "feature/x", "main", Ct);

        Assert.Equal("base-sha", sha);
        var create = _github.Single(HttpMethod.Post).Json;
        Assert.Equal("refs/heads/feature/x", create.GetProperty("ref").GetString());
        Assert.Equal("base-sha", create.GetProperty("sha").GetString());
    }

    [Fact]
    public async Task CreateBranchAsync_WhenBranchExists_ShouldReturnItsShaWithoutCreatingIt()
    {
        _github.On(HttpMethod.Get, "/repos/acme/billing/git/refs/heads/feature/x",
            FakeGitHub.Reference("refs/heads/feature/x", "existing-sha"));

        var sha = await CodeHost().CreateBranchAsync(Repo, "feature/x", "main", Ct);

        Assert.Equal("existing-sha", sha);
        Assert.DoesNotContain(_github.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task CommitFileAsync_WhenFileIsNew_ShouldCreateItOnTheBranch()
    {
        _github.On(HttpMethod.Put, "/repos/acme/billing/contents/docs/prd-x.md",
            FakeGitHub.ContentChange("docs/prd-x.md"), HttpStatusCode.Created);

        await CodeHost().CommitFileAsync(Repo, "main", "docs/prd-x.md", "# PRD", "Add PRD", Ct);

        var put = _github.Single(HttpMethod.Put).Json;
        Assert.Equal("Add PRD", put.GetProperty("message").GetString());
        Assert.Equal("main", put.GetProperty("branch").GetString());
        Assert.Equal("# PRD", Encoding.UTF8.GetString(Convert.FromBase64String(put.GetProperty("content").GetString()!)));
        Assert.False(put.TryGetProperty("sha", out _));
    }

    [Fact]
    public async Task CommitFileAsync_WhenFileExists_ShouldUpdateItWithItsCurrentSha()
    {
        _github
            .On(HttpMethod.Get, "/repos/acme/billing/contents/docs/prd-x.md?ref=main",
                new[] { FakeGitHub.File("docs/prd-x.md", "old-sha", "# Old") })
            .On(HttpMethod.Put, "/repos/acme/billing/contents/docs/prd-x.md", FakeGitHub.ContentChange("docs/prd-x.md"));

        await CodeHost().CommitFileAsync(Repo, "main", "docs/prd-x.md", "# New", "Update PRD", Ct);

        Assert.Equal("old-sha", _github.Single(HttpMethod.Put).Json.GetProperty("sha").GetString());
    }

    [Fact]
    public async Task CreatePullRequestAsync_WhenCreated_ShouldReturnItsNumber()
    {
        _github.On(HttpMethod.Post, "/repos/acme/billing/pulls", FakeGitHub.PullRequest(42), HttpStatusCode.Created);

        var number = await CodeHost().CreatePullRequestAsync(Repo, "feature/x", "main", "Add export", "Closes #7", Ct);

        Assert.Equal(42, number);
        var body = _github.Single(HttpMethod.Post).Json;
        Assert.Equal("feature/x", body.GetProperty("head").GetString());
        Assert.Equal("main", body.GetProperty("base").GetString());
        Assert.Equal("Closes #7", body.GetProperty("body").GetString());
    }

    [Fact]
    public async Task CreateIssueAsync_WhenCreated_ShouldSendTheLabelsAndReturnTheNumber()
    {
        _github.On(HttpMethod.Post, "/repos/acme/billing/issues", FakeGitHub.Issue(7, "Task", "Body"), HttpStatusCode.Created);

        var number = await CodeHost().CreateIssueAsync(Repo, "Task", "Body", ["zephyrus", "BE"], Ct);

        Assert.Equal(7, number);
        var labels = _github.Single(HttpMethod.Post).Json.GetProperty("labels").EnumerateArray().Select(l => l.GetString());
        Assert.Equal(["zephyrus", "BE"], labels);
    }

    [Fact]
    public async Task GetFileContentAsync_WhenFileExists_ShouldReturnItDecoded()
    {
        _github.On(HttpMethod.Get, "/repos/acme/billing/contents/docs/adr-x.md?ref=main",
            new[] { FakeGitHub.File("docs/adr-x.md", "sha", "# ADR\nDecision.") });

        var content = await CodeHost().GetFileContentAsync(Repo, "main", "docs/adr-x.md", Ct);

        Assert.Equal("# ADR\nDecision.", content);
    }

    [Fact]
    public async Task GetFileContentAsync_WhenFileIsMissing_ShouldReturnNull()
    {
        Assert.Null(await CodeHost().GetFileContentAsync(Repo, "main", "docs/missing.md", Ct));
    }

    [Fact]
    public async Task GetIssueContentAsync_WhenIssueHasNoBody_ShouldReturnAnEmptyBody()
    {
        _github.On(HttpMethod.Get, "/repos/acme/billing/issues/7", FakeGitHub.Issue(7, "Add export", null));

        var (title, body) = await CodeHost().GetIssueContentAsync(Repo, 7, Ct);

        Assert.Equal("Add export", title);
        Assert.Equal(string.Empty, body);
    }

    [Fact]
    public async Task Requests_WhenCalled_ShouldAuthenticateWithTheProjectToken()
    {
        _github.On(HttpMethod.Get, "/repos/acme/billing/issues/7", FakeGitHub.Issue(7, "T", "B"));

        await CodeHost("ghp_secret").GetIssueContentAsync(Repo, 7, Ct);

        Assert.EndsWith("ghp_secret", _github.Requests.Single().Authorization);
    }

    [Fact]
    public async Task Requests_WhenGitHubFails_ShouldSurfaceTheError()
    {
        _github.On(HttpMethod.Post, "/repos/acme/billing/issues", new { message = "Server Error" },
            HttpStatusCode.InternalServerError);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => CodeHost().CreateIssueAsync(Repo, "T", "B", [], Ct));

        Assert.Contains("Server Error", error.Message);
    }

    [Theory]
    [InlineData("no-slash")]
    [InlineData("")]
    public async Task Requests_WhenRepositoryIsNotOwnerSlashName_ShouldThrowBeforeCallingGitHub(string repo)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CodeHost().GetIssueContentAsync(repo, 1, Ct));
        Assert.Empty(_github.Requests);
    }
}
