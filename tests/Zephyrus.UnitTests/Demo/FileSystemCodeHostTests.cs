using Zephyrus.Infrastructure.Demo;

namespace Zephyrus.UnitTests.Demo;

public sealed class FileSystemCodeHostTests : IDisposable
{
    private const string Repo = "acme/billing";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "zephyrus-demo-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemCodeHost _sut;

    public FileSystemCodeHostTests()
    {
        _sut = new FileSystemCodeHost(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task GetFileContentAsync_WhenFileWasCommitted_ShouldReturnItsContent()
    {
        await _sut.CommitFileAsync(Repo, "main", "docs/prd/a.md", "# PRD", "msg", Ct);

        Assert.Equal("# PRD", await _sut.GetFileContentAsync(Repo, "main", "docs/prd/a.md", Ct));
        Assert.True(File.Exists(Path.Combine(_root, "acme", "billing", "main", "docs", "prd", "a.md")));
    }

    [Fact]
    public async Task GetFileContentAsync_WhenFileIsOnlyOnMain_ShouldReadThroughFromABranch()
    {
        await _sut.CommitFileAsync(Repo, "main", "CODEBASE.md", "map", "msg", Ct);
        await _sut.CreateBranchAsync(Repo, "feature/x/1", "main", Ct);

        Assert.Equal("map", await _sut.GetFileContentAsync(Repo, "feature/x/1", "CODEBASE.md", Ct));
    }

    [Fact]
    public async Task GetFileContentAsync_WhenFileIsMissing_ShouldReturnNull()
    {
        Assert.Null(await _sut.GetFileContentAsync(Repo, "main", "nope.md", Ct));
    }

    [Fact]
    public async Task CreateIssueAsync_WhenCalledRepeatedly_ShouldNumberIssuesAndKeepTheirContent()
    {
        var first = await _sut.CreateIssueAsync(Repo, "First", "Body 1", new[] { "zephyrus" }, Ct);
        var second = await _sut.CreateIssueAsync(Repo, "Second", "Body 2", Array.Empty<string>(), Ct);

        Assert.Equal((1, 2), (first, second));
        Assert.Equal(("Second", "Body 2"), await _sut.GetIssueContentAsync(Repo, 2, Ct));
    }

    [Fact]
    public async Task CreatePullRequestAsync_WhenCalledRepeatedly_ShouldNumberPullRequestsSeparatelyFromIssues()
    {
        await _sut.CreateIssueAsync(Repo, "Issue", "Body", Array.Empty<string>(), Ct);

        var pr = await _sut.CreatePullRequestAsync(Repo, "feature/x/1", "main", "Title", "Body", Ct);

        Assert.Equal(1, pr);
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("docs/../../outside.md")]
    [InlineData(".zephyrus/issues/1.json")]
    public async Task CommitFileAsync_WhenPathEscapesTheRepository_ShouldThrow(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CommitFileAsync(Repo, "main", path, "x", "msg", Ct));
    }

    [Fact]
    public async Task CommitFileAsync_WhenRepositorySlugEscapesTheRoot_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CommitFileAsync("../etc", "main", "a.md", "x", "msg", Ct));
    }
}
