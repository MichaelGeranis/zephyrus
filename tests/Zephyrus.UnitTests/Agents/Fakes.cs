using Zephyrus.Core.Interfaces;

namespace Zephyrus.UnitTests.Agents;

internal sealed class FakeLanguageModel : ILanguageModel
{
    private readonly string _response;
    public string LastSystemPrompt { get; private set; } = string.Empty;
    public string LastUserMessage { get; private set; } = string.Empty;

    public FakeLanguageModel(string response) => _response = response;

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        LastSystemPrompt = systemPrompt;
        LastUserMessage = userMessage;
        return Task.FromResult(_response);
    }

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => GenerateAsync(systemPrompt, userMessage, ct);
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
