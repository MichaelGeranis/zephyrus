using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.Demo;

/// <summary>
/// Hands out the local demo code host. The project's token is ignored: in demo
/// mode nothing is sent to GitHub.
/// </summary>
public sealed class DemoCodeHostFactory : ICodeHostFactory
{
    private readonly FileSystemCodeHost _codeHost;

    public DemoCodeHostFactory(string repositoryRoot)
    {
        _codeHost = new FileSystemCodeHost(repositoryRoot);
    }

    public ICodeHost Create(string token) => _codeHost;
}
