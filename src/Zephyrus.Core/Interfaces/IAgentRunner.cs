namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Generic agent contract. Every agent takes typed input and produces typed output.
/// </summary>
public interface IAgent<TInput, TOutput>
    where TInput : class
    where TOutput : class
{
    Task<TOutput> RunAsync(TInput input, CancellationToken ct = default);
}
