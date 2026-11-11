using Zephyrus.Core.Exceptions;
using Zephyrus.Core.Pipeline;

namespace Zephyrus.UnitTests;

public class GeneratedFilePolicyTests
{
    [Theory]
    [InlineData("src/Feature/FeatureService.cs")]
    [InlineData("tests/Feature/FeatureServiceTests.cs")]
    [InlineData("README.md")]
    [InlineData("docs/feature-guide.md")]
    [InlineData("src/.gitignore")]
    [InlineData("web/app/page.tsx")]
    public void Violation_WhenPathIsOrdinaryCode_ShouldBeNull(string path)
    {
        Assert.Null(GeneratedFilePolicy.Violation(path));
    }

    [Theory]
    [InlineData(".github/workflows/deploy.yml")]
    [InlineData(".GitHub/Workflows/deploy.yml")]
    [InlineData(".github/CODEOWNERS")]
    [InlineData(".github/dependabot.yml")]
    [InlineData(".circleci/config.yml")]
    [InlineData(".gitlab-ci.yml")]
    [InlineData("azure-pipelines.yml")]
    [InlineData("Jenkinsfile")]
    [InlineData("CODEOWNERS")]
    [InlineData("docs/CODEOWNERS")]
    [InlineData(".git/hooks/pre-commit")]
    [InlineData(".gitmodules")]
    [InlineData("vendor/lib/.git")]
    [InlineData(".husky/pre-commit")]
    [InlineData(".devcontainer/devcontainer.json")]
    [InlineData(".vscode/tasks.json")]
    public void Violation_WhenPathRunsCodeOutsideReview_ShouldBeRejected(string path)
    {
        Assert.NotNull(GeneratedFilePolicy.Violation(path));
    }

    [Theory]
    [InlineData("docs/prd/3f2a.md")]
    [InlineData("docs/adr/3f2a.md")]
    [InlineData("docs/tasks/3f2a.md")]
    [InlineData("docs/qa/3f2a.md")]
    public void Violation_WhenPathIsAnApprovedArtifact_ShouldBeRejected(string path)
    {
        Assert.NotNull(GeneratedFilePolicy.Violation(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("src\\Feature.cs")]
    [InlineData("../outside.cs")]
    [InlineData("src/../.github/workflows/x.yml")]
    [InlineData("src/./Feature.cs")]
    [InlineData("src//Feature.cs")]
    [InlineData("src/Feature.cs\n")]
    public void Violation_WhenPathIsMalformedOrEscapesTheRepository_ShouldBeRejected(string path)
    {
        Assert.NotNull(GeneratedFilePolicy.Violation(path));
    }

    [Fact]
    public void EnsureAllowed_WhenAllPathsAreAllowed_ShouldNotThrow()
    {
        GeneratedFilePolicy.EnsureAllowed("Code", new[] { "src/A.cs", "tests/ATests.cs" });
    }

    [Fact]
    public void EnsureAllowed_WhenSomePathsAreRejected_ShouldNameEveryRejectedPath()
    {
        var error = Assert.Throws<GeneratedFileRejectedException>(() =>
            GeneratedFilePolicy.EnsureAllowed("Code", new[] { "src/A.cs", ".github/workflows/x.yml", "../b.cs" }));

        Assert.Equal(2, error.RejectedPaths.Count);
        Assert.Contains(".github/workflows/x.yml", error.Message);
        Assert.Contains("../b.cs", error.Message);
        Assert.DoesNotContain("src/A.cs", error.Message);
        Assert.StartsWith("The Code Agent", error.Message);
    }
}
