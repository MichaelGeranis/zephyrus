using Zephyrus.Core.Agents;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// A language model the agents call: the Claude API or a local model, optionally
/// chained with a fallback. Implemented in Infrastructure.
/// </summary>
public interface ILanguageModel
{
    Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
    Task<string> GenerateAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default);
    Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<ConversationMessage> messages, int maxTokens, CancellationToken ct = default);
}
