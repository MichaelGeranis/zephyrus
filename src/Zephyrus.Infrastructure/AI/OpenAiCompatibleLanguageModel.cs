using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.Telemetry;

namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// <see cref="ILanguageModel"/> for any server that speaks the OpenAI chat
/// completions API. That covers the common ways to run a model locally:
/// Ollama, LM Studio, vLLM and llama.cpp's server.
/// </summary>
public sealed class OpenAiCompatibleLanguageModel : ILanguageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly LocalModelOptions _options;
    private readonly LanguageModelUsageTracker _usage;
    private readonly string _model;

    public OpenAiCompatibleLanguageModel(
        HttpClient httpClient,
        IOptions<LocalModelOptions> options,
        LanguageModelUsageTracker usage,
        string? model = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _model = model ?? _options.Model;
        _usage = usage;

        _httpClient.BaseAddress ??= new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = _options.Timeout;
        if (!string.IsNullOrEmpty(_options.ApiKey))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        => GenerateAsync(systemPrompt, userMessage, _options.MaxTokens, ct);

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => SendAsync(systemPrompt, new[] { new ConversationMessage("user", userMessage) }, maxTokens, ct);

    public Task<string> GenerateAsync(
        string systemPrompt, IReadOnlyList<ConversationMessage> messages, int maxTokens, CancellationToken ct = default)
        => SendAsync(systemPrompt, messages, maxTokens, ct);

    private async Task<string> SendAsync(
        string systemPrompt, IReadOnlyList<ConversationMessage> messages, int maxTokens, CancellationToken ct)
    {
        var request = new ChatRequest
        {
            Model = _model,
            // Agents ask for up to the Claude limit; local servers reject more than they allow.
            MaxTokens = Math.Min(maxTokens, _options.MaxTokens),
            Messages = new[] { new ChatMessage { Role = "system", Content = systemPrompt } }
                .Concat(messages.Select(m => new ChatMessage { Role = m.Role, Content = m.Content }))
                .ToArray()
        };

        // "openai" names the API dialect; the model itself runs wherever BaseUrl points.
        using var activity = ZephyrusTelemetry.StartModelCall("openai", _model);
        try
        {
            return await SendRequestAsync(request, activity, ct);
        }
        catch (Exception ex)
        {
            ZephyrusTelemetry.RecordError(activity, ex);
            throw;
        }
    }

    private async Task<string> SendRequestAsync(ChatRequest request, System.Diagnostics.Activity? activity, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("chat/completions", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Local model at {_options.BaseUrl} returned {(int)response.StatusCode}: {Shorten(body)}",
                null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Local model returned an empty response.");

        if (result.Usage is not null)
        {
            _usage.Record(result.Model ?? _model, result.Usage.PromptTokens, result.Usage.CompletionTokens, free: true);
            ZephyrusTelemetry.RecordModelResponse(activity, result.Model, result.Usage.PromptTokens, result.Usage.CompletionTokens);
        }

        var choice = result.Choices.FirstOrDefault()
            ?? throw new InvalidOperationException("Local model returned no choices.");

        if (choice.FinishReason == "length")
            throw new InvalidOperationException(
                $"Local model response was truncated at {request.MaxTokens} tokens. " +
                "Raise LocalModel:MaxTokens, or the model's context length on the server.");

        if (string.IsNullOrWhiteSpace(choice.Message?.Content))
            throw new InvalidOperationException("Local model returned an empty answer.");

        return choice.Message.Content;
    }

    private static string Shorten(string text) => text.Length <= 300 ? text : text[..300] + "…";

    private sealed class ChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
        public ChatMessage[] Messages { get; set; } = Array.Empty<ChatMessage>();
    }

    private sealed class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string? Content { get; set; }
    }

    private sealed class ChatResponse
    {
        public string? Model { get; set; }
        public ChatChoice[] Choices { get; set; } = Array.Empty<ChatChoice>();
        public ChatUsage? Usage { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private sealed class ChatUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
    }
}
