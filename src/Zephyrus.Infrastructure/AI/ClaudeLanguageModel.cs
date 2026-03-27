using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<ClaudeLanguageModel> _logger;

    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ClaudeLanguageModel(HttpClient httpClient, IOptions<ClaudeLanguageModelOptions> options, ILogger<ClaudeLanguageModel> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

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

        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            response = await _httpClient.PostAsJsonAsync("/v1/messages", request, JsonOptions, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                response.StatusCode == HttpStatusCode.InternalServerError ||
                response.StatusCode == HttpStatusCode.BadGateway ||
                response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                if (attempt == MaxRetries)
                    break;

                var delay = GetRetryDelay(response, attempt);
                _logger.LogWarning(
                    "Claude API returned {StatusCode}. Retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                    (int)response.StatusCode, delay.TotalSeconds, attempt + 1, MaxRetries);
                await Task.Delay(delay, ct);
                continue;
            }

            break;
        }

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

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.TryGetValues("retry-after", out var values) &&
            int.TryParse(values.FirstOrDefault(), out var retryAfterSeconds))
        {
            return TimeSpan.FromSeconds(retryAfterSeconds);
        }

        return InitialBackoff * Math.Pow(2, attempt);
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
