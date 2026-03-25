using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.GitHub;

/// <summary>
/// Creates <see cref="GitHubCodeHost"/> instances with per-project tokens.
/// </summary>
public sealed class GitHubCodeHostFactory : ICodeHostFactory
{
    public ICodeHost Create(string token)
    {
        return new GitHubCodeHost(token);
    }
}
