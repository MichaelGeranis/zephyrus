using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.Telemetry;

namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// ILanguageModel implementation that calls the Anthropic Messages API via HttpClient.
/// </summary>
public sealed class ClaudeLanguageModel : ILanguageModel
{
    private const string ServerSideFallbackBeta = "server-side-fallback-2026-07-01";

    private readonly HttpClient _httpClient;
    private readonly ClaudeLanguageModelOptions _options;
    private readonly LanguageModelUsageTracker _usage;
    private readonly string _model;
    private readonly ILogger<ClaudeLanguageModel> _logger;

    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ClaudeLanguageModel(
        HttpClient httpClient,
        IOptions<ClaudeLanguageModelOptions> options,
        LanguageModelUsageTracker usage,
        ILogger<ClaudeLanguageModel> logger,
        string? model = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _model = model ?? _options.Model;
        _usage = usage;
        _logger = logger;

        _httpClient.BaseAddress ??= new Uri("https://api.anthropic.com");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        if (_options.ServerSideFallback)
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-beta", ServerSideFallbackBeta);
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
            Model = _model,
            MaxTokens = maxTokens,
            System = systemPrompt,
            Messages = messages,
            Fallbacks = _options.ServerSideFallback ? "default" : null,
            OutputConfig = string.IsNullOrWhiteSpace(_options.Effort) ? null : new ClaudeOutputConfig { Effort = _options.Effort }
        };

        using var activity = ZephyrusTelemetry.StartModelCall("anthropic", _model);
        try
        {
            return await SendWithRetriesAsync(request, maxTokens, activity, ct);
        }
        catch (Exception ex)
        {
            ZephyrusTelemetry.RecordError(activity, ex);
            throw;
        }
    }

    private async Task<string> SendWithRetriesAsync(ClaudeRequest request, int maxTokens, System.Diagnostics.Activity? activity, CancellationToken ct)
    {
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

        // Billed whether or not the answer is usable, so record it first.
        if (result.Usage is not null)
        {
            _usage.Record(result.Model ?? _model, result.Usage.InputTokens, result.Usage.OutputTokens, free: false);
            ZephyrusTelemetry.RecordModelResponse(activity, result.Model, result.Usage.InputTokens, result.Usage.OutputTokens);
        }

        // A refusal is an HTTP 200 with no usable answer; check before reading content.
        if (result.StopReason == "refusal")
            throw new InvalidOperationException(
                "Claude declined this request (stop_reason: refusal). Review the feature prompt or re-run the step.");

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
        public string? Fallbacks { get; set; }
        public ClaudeOutputConfig? OutputConfig { get; set; }
    }

    private sealed class ClaudeOutputConfig
    {
        public string Effort { get; set; } = string.Empty;
    }

    private sealed class ClaudeUsage
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
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
        public string? Model { get; set; }
        public ClaudeUsage? Usage { get; set; }
    }

    private sealed class ClaudeContentBlock
    {
        public string Type { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
