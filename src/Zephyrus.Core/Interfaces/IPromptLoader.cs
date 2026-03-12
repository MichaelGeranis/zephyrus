namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Loads system prompts for agents from external storage.
/// Implemented in Infrastructure — agents depend on this abstraction.
/// </summary>
public interface IPromptLoader
{
    Task<string> LoadAsync(string agentName, CancellationToken ct = default);
}
