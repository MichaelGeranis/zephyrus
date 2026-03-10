using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
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

    public async Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var request = new ClaudeRequest
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = systemPrompt,
            Messages = new[]
            {
                new ClaudeMessage { Role = "user", Content = userMessage }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/messages", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Claude API returned null response.");

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
    }

    private sealed class ClaudeContentBlock
    {
        public string Type { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
