using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zephyrus.Infrastructure.AI;

namespace Zephyrus.UnitTests.AI;

public class ProviderClientTests
{
    private const string LocalAnswer = """
        {"model":"llama3.1:8b","choices":[{"message":{"role":"assistant","content":"# PRD"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":120,"completion_tokens":30}}
        """;

    private static OpenAiCompatibleLanguageModel Local(StubHttpHandler handler, LanguageModelUsageTracker tracker, string? model = null) =>
        new(new HttpClient(handler), Options.Create(new LocalModelOptions { BaseUrl = "http://ollama:11434/v1", MaxTokens = 4096 }), tracker, model);

    private static ClaudeLanguageModel Claude(StubHttpHandler handler, LanguageModelUsageTracker tracker) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.test") },
            Options.Create(new ClaudeLanguageModelOptions { ApiKey = "test-key" }), tracker, NullLogger<ClaudeLanguageModel>.Instance);

    [Fact]
    public async Task LocalModel_WhenCalled_ShouldSendSystemPromptFirstAndRecordFreeUsage()
    {
        var handler = new StubHttpHandler(LocalAnswer);
        var tracker = LanguageModelTestSupport.Tracker();

        var answer = await Local(handler, tracker, "llama3.1:8b").GenerateAsync("You are the PRD Agent", "Feature", 16000, Ct);

        Assert.Equal("# PRD", answer);
        Assert.EndsWith("/v1/chat/completions", handler.LastRequest!.RequestUri!.AbsoluteUri);
        using var body = JsonDocument.Parse(handler.LastRequestBody);
        Assert.Equal("llama3.1:8b", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(4096, body.RootElement.GetProperty("max_tokens").GetInt32()); // capped at the local limit
        Assert.Equal("system", body.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());

        var usage = tracker.Drain()!;
        Assert.Equal((120, 30, 0m), (usage.InputTokens, usage.OutputTokens, usage.EstimatedCostUsd!.Value));
    }

    [Fact]
    public async Task LocalModel_WhenAnswerIsCutOff_ShouldThrowSoTheFallbackCanTakeOver()
    {
        var handler = new StubHttpHandler(LocalAnswer.Replace("\"stop\"", "\"length\""));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Local(handler, LanguageModelTestSupport.Tracker()).GenerateAsync("s", "u", Ct));
    }

    [Fact]
    public async Task LocalModel_WhenServerErrors_ShouldThrowHttpRequestException()
    {
        var handler = new StubHttpHandler("""{"error":"model 'llama3.1:8b' not found"}""", HttpStatusCode.NotFound);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Local(handler, LanguageModelTestSupport.Tracker()).GenerateAsync("s", "u", Ct));
        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public async Task Claude_WhenCalled_ShouldOptIntoServerSideFallbackAndRecordPricedUsage()
    {
        var handler = new StubHttpHandler("""
            {"model":"claude-opus-5-5","stop_reason":"end_turn",
             "content":[{"type":"thinking","thinking":""},{"type":"text","text":"# ADR"}],
             "usage":{"input_tokens":1000000,"output_tokens":50000}}
            """);
        var tracker = LanguageModelTestSupport.Tracker(("claude-opus-5-5", 4m, 20m));

        var answer = await Claude(handler, tracker).GenerateAsync("s", "u", Ct);

        Assert.Equal("# ADR", answer);
        Assert.Contains("server-side-fallback-2026-07-01", handler.LastRequest!.Headers.GetValues("anthropic-beta"));
        using var body = JsonDocument.Parse(handler.LastRequestBody);
        Assert.Equal("claude-opus-5-5", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("default", body.RootElement.GetProperty("fallbacks").GetString());
        Assert.Equal("high", body.RootElement.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal(5m, tracker.Drain()!.EstimatedCostUsd); // $4 + $1
    }

    [Fact]
    public async Task Claude_WhenRequestIsRefused_ShouldThrowAndStillRecordTheBilledUsage()
    {
        var handler = new StubHttpHandler("""
            {"model":"claude-opus-5-5","stop_reason":"refusal","content":[],
             "usage":{"input_tokens":500,"output_tokens":0}}
            """);
        var tracker = LanguageModelTestSupport.Tracker(("claude-opus-5-5", 4m, 20m));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Claude(handler, tracker).GenerateAsync("s", "u", Ct));

        Assert.Contains("refusal", error.Message);
        Assert.Equal(500, tracker.Drain()!.InputTokens);
    }
}
