using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.GitHub;

/// <summary>
/// Creates <see cref="GitHubCodeHost"/> instances. The project's stored token
/// is the fallback: a repository the Zephyrus GitHub App is installed on uses
/// the App's installation tokens instead.
/// </summary>
public sealed class GitHubCodeHostFactory : ICodeHostFactory
{
    private readonly GitHubAppTokenSource _app;

    public GitHubCodeHostFactory(GitHubAppTokenSource app)
    {
        _app = app;
    }

    public ICodeHost Create(string token) => new GitHubCodeHost(token, _app);
}
