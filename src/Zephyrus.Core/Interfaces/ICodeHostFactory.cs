namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Factory that creates <see cref="ICodeHost"/> instances using a per-project token.
/// </summary>
public interface ICodeHostFactory
{
    ICodeHost Create(string token);
}
