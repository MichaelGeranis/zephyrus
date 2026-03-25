using Zephyrus.Core.Interfaces;

namespace Zephyrus.IntegrationTests.Fakes;

/// <summary>
/// Returns a shared <see cref="FakeCodeHost"/> for any token.
/// </summary>
public sealed class FakeCodeHostFactory : ICodeHostFactory
{
    private readonly FakeCodeHost _codeHost;

    public FakeCodeHostFactory(FakeCodeHost codeHost)
    {
        _codeHost = codeHost;
    }

    public ICodeHost Create(string token) => _codeHost;
}
