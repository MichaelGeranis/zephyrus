using Zephyrus.Core.Agents;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Abstraction over the Claude API. Implemented in Infrastructure via HttpClient.
/// </summary>
public interface ILanguageModel
{
    Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
    Task<string> GenerateAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default);
    Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<ConversationMessage> messages, int maxTokens, CancellationToken ct = default);
}
