using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.UnitTests.Agents;

internal sealed class FakeLanguageModel : ILanguageModel
{
    private readonly string _response;
    public string LastSystemPrompt { get; private set; } = string.Empty;
    public string LastUserMessage { get; private set; } = string.Empty;
    public IReadOnlyList<ConversationMessage>? LastMessages { get; private set; }

    public FakeLanguageModel(string response) => _response = response;

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        LastSystemPrompt = systemPrompt;
        LastUserMessage = userMessage;
        return Task.FromResult(_response);
    }

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => GenerateAsync(systemPrompt, userMessage, ct);

    public Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<ConversationMessage> messages, int maxTokens, CancellationToken ct = default)
    {
        LastSystemPrompt = systemPrompt;
        LastMessages = messages;
        LastUserMessage = messages[^1].Content;
        return Task.FromResult(_response);
    }
}

internal sealed class FakePromptLoader : IPromptLoader
{
    private readonly string _content;
    public string LastLoadedName { get; private set; } = string.Empty;

    public FakePromptLoader(string content) => _content = content;

    public Task<string> LoadAsync(string name, CancellationToken ct = default)
    {
        LastLoadedName = name;
        return Task.FromResult(_content);
    }
}
