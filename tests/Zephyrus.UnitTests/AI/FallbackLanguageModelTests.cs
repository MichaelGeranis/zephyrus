using Microsoft.Extensions.Logging.Abstractions;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.AI;

namespace Zephyrus.UnitTests.AI;

public class FallbackLanguageModelTests
{
    private sealed class ScriptedModel : ILanguageModel
    {
        private readonly Exception? _error;
        private readonly string _answer;

        public ScriptedModel(string answer, Exception? error = null)
        {
            _answer = answer;
            _error = error;
        }

        public int Calls { get; private set; }

        public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        {
            Calls++;
            return _error is null ? Task.FromResult(_answer) : Task.FromException<string>(_error);
        }

        public Task<string> GenerateAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
            => GenerateAsync(systemPrompt, userMessage, ct);

        public Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<ConversationMessage> messages, int maxTokens, CancellationToken ct = default)
            => GenerateAsync(systemPrompt, messages[^1].Content, ct);
    }

    private static FallbackLanguageModel Chain(ILanguageModel primary, ILanguageModel? fallback, bool preferFallback = false) =>
        new(primary, fallback, new LanguageModelRouting { PreferFallback = preferFallback }, NullLogger<FallbackLanguageModel>.Instance);

    [Fact]
    public async Task GenerateAsync_WhenPrimaryAnswers_ShouldNotCallTheFallback()
    {
        var primary = new ScriptedModel("local");
        var fallback = new ScriptedModel("opus");

        Assert.Equal("local", await Chain(primary, fallback).GenerateAsync("s", "u", Ct));
        Assert.Equal(0, fallback.Calls);
    }

    [Fact]
    public async Task GenerateAsync_WhenPrimaryIsUnreachable_ShouldUseTheFallback()
    {
        var primary = new ScriptedModel("local", new HttpRequestException("Connection refused"));
        var fallback = new ScriptedModel("opus");

        Assert.Equal("opus", await Chain(primary, fallback).GenerateAsync("s", "u", Ct));
    }

    [Fact]
    public async Task GenerateAsync_WhenRetryPrefersTheFallback_ShouldSkipThePrimary()
    {
        var primary = new ScriptedModel("local");
        var fallback = new ScriptedModel("opus");

        Assert.Equal("opus", await Chain(primary, fallback, preferFallback: true).GenerateAsync("s", "u", Ct));
        Assert.Equal(0, primary.Calls);
    }

    [Fact]
    public async Task GenerateAsync_WhenThereIsNoFallback_ShouldRethrow()
    {
        var primary = new ScriptedModel("local", new HttpRequestException("down"));

        await Assert.ThrowsAsync<HttpRequestException>(() => Chain(primary, null).GenerateAsync("s", "u", Ct));
    }

    [Fact]
    public async Task GenerateAsync_WhenCallerCancelled_ShouldNotFallBack()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var primary = new ScriptedModel("local", new OperationCanceledException());
        var fallback = new ScriptedModel("opus");

        await Assert.ThrowsAsync<OperationCanceledException>(() => Chain(primary, fallback).GenerateAsync("s", "u", cts.Token));
        Assert.Equal(0, fallback.Calls);
    }
}
