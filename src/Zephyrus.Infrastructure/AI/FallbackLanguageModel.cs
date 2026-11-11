using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// Sends each call to the primary model and, if that call fails, to the
/// fallback. Also goes straight to the fallback when
/// <see cref="LanguageModelRouting.PreferFallback"/> is set: the job worker sets
/// it on a retry, because a job that failed on the primary model (often on
/// malformed output, which this class cannot detect) should not repeat it.
/// </summary>
public sealed class FallbackLanguageModel : ILanguageModel
{
    private readonly ILanguageModel _primary;
    private readonly ILanguageModel? _fallback;
    private readonly LanguageModelRouting _routing;
    private readonly ILogger<FallbackLanguageModel> _logger;

    public FallbackLanguageModel(
        ILanguageModel primary,
        ILanguageModel? fallback,
        LanguageModelRouting routing,
        ILogger<FallbackLanguageModel> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _routing = routing;
        _logger = logger;
    }

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        => RunAsync(m => m.GenerateAsync(systemPrompt, userMessage, ct), ct);

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => RunAsync(m => m.GenerateAsync(systemPrompt, userMessage, maxTokens, ct), ct);

    public Task<string> GenerateAsync(
        string systemPrompt, IReadOnlyList<ConversationMessage> messages, int maxTokens, CancellationToken ct = default)
        => RunAsync(m => m.GenerateAsync(systemPrompt, messages, maxTokens, ct), ct);

    private async Task<string> RunAsync(Func<ILanguageModel, Task<string>> call, CancellationToken ct)
    {
        if (_fallback is not null && _routing.PreferFallback)
        {
            Activity.Current?.AddEvent(new ActivityEvent("gen_ai.fallback", tags: new ActivityTagsCollection { ["reason"] = "retry" }));
            return await call(_fallback);
        }

        try
        {
            return await call(_primary);
        }
        catch (Exception ex) when (_fallback is not null && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Primary language model failed; using the fallback model.");
            Activity.Current?.AddEvent(new ActivityEvent("gen_ai.fallback", tags: new ActivityTagsCollection { ["reason"] = ex.GetType().Name }));
            return await call(_fallback);
        }
    }
}
