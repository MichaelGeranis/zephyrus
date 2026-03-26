using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// ILanguageModel implementation that calls the Anthropic Messages API via HttpClient.
/// </summary>
public sealed class ClaudeLanguageModel : ILanguageModel
{
    private readonly HttpClient _httpClient;
    private readonly ClaudeLanguageModelOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ClaudeLanguageModel(HttpClient httpClient, IOptions<ClaudeLanguageModelOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        _httpClient.BaseAddress ??= new Uri("https://api.anthropic.com");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");
    }

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        => GenerateAsync(systemPrompt, userMessage, _options.MaxTokens, ct);

    public async Task<string> GenerateAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        var messages = new[] { new ClaudeMessage { Role = "user", Content = userMessage } };
        return await SendRequestAsync(systemPrompt, messages, maxTokens, ct);
    }

    public async Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<ConversationMessage> messages, int maxTokens, CancellationToken ct = default)
    {
        var claudeMessages = messages
            .Select(m => new ClaudeMessage { Role = m.Role, Content = m.Content })
            .ToArray();
        return await SendRequestAsync(systemPrompt, claudeMessages, maxTokens, ct);
    }

    private async Task<string> SendRequestAsync(string systemPrompt, ClaudeMessage[] messages, int maxTokens, CancellationToken ct)
    {
        var request = new ClaudeRequest
        {
            Model = _options.Model,
            MaxTokens = maxTokens,
            System = systemPrompt,
            Messages = messages
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/messages", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Claude API returned null response.");

        if (result.StopReason == "max_tokens")
            throw new InvalidOperationException(
                $"Claude API response was truncated (hit max_tokens limit of {maxTokens}). " +
                "The output was cut off before completion. Consider increasing the token limit.");

        var textBlock = result.Content.FirstOrDefault(c => c.Type == "text")
            ?? throw new InvalidOperationException("Claude API response contained no text content.");

        return textBlock.Text;
    }

    private sealed class ClaudeRequest
    {
        public string Model { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
        public string System { get; set; } = string.Empty;
        public ClaudeMessage[] Messages { get; set; } = Array.Empty<ClaudeMessage>();
    }

    private sealed class ClaudeMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ClaudeResponse
    {
        public ClaudeContentBlock[] Content { get; set; } = Array.Empty<ClaudeContentBlock>();
        public string StopReason { get; set; } = string.Empty;
    }

    private sealed class ClaudeContentBlock
    {
        public string Type { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
