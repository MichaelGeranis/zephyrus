using Microsoft.Extensions.Options;

namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// Collects the model and token counts of language model calls made in one DI
/// scope (an HTTP request or an agent job) until the agent run they belong to
/// is stored.
/// </summary>
/// <remarks>
/// Each agent run makes its model calls and is then saved as an
/// <c>AgentInvocation</c> in the same scope, one after the other, so the usage
/// recorded since the last save belongs to the run being saved. This keeps the
/// agents and use cases unaware of usage accounting.
/// </remarks>
public sealed class LanguageModelUsageTracker
{
    private readonly ModelPricingOptions _pricing;
    private readonly List<(string Model, int Input, int Output, bool Free)> _calls = new();

    public LanguageModelUsageTracker(IOptions<ModelPricingOptions> pricing)
    {
        _pricing = pricing.Value;
    }

    public void Record(string model, int inputTokens, int outputTokens, bool free)
    {
        lock (_calls) _calls.Add((model, inputTokens, outputTokens, free));
    }

    /// <summary>Returns the usage recorded since the last call and clears it.</summary>
    public RecordedUsage? Drain()
    {
        List<(string Model, int Input, int Output, bool Free)> calls;
        lock (_calls)
        {
            if (_calls.Count == 0)
                return null;
            calls = _calls.ToList();
            _calls.Clear();
        }

        decimal? cost = 0m;
        foreach (var call in calls)
        {
            var callCost = Estimate(call.Model, call.Input, call.Output, call.Free);
            cost = cost is null || callCost is null ? null : cost + callCost;
        }

        // A run that fell back names both models, primary first.
        var models = string.Join(" → ", calls.Select(c => c.Model).Distinct());
        return new RecordedUsage(models, calls.Sum(c => c.Input), calls.Sum(c => c.Output), cost);
    }

    internal decimal? Estimate(string model, int inputTokens, int outputTokens, bool free)
    {
        if (free)
            return 0m;

        // The API may answer with a dated model ID; match the longest configured prefix.
        var price = _pricing.Models
            .Where(p => model.StartsWith(p.Key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Key.Length)
            .Select(p => p.Value)
            .FirstOrDefault();

        if (price is null)
            return null;

        return Math.Round(
            (inputTokens * price.InputPerMillion + outputTokens * price.OutputPerMillion) / 1_000_000m, 6);
    }
}

public sealed record RecordedUsage(string Model, int InputTokens, int OutputTokens, decimal? EstimatedCostUsd);

/// <summary>
/// Lets the job worker ask for the fallback model directly. A retry after a
/// failed attempt goes to the stronger model instead of repeating the same
/// failure on the primary.
/// </summary>
public sealed class LanguageModelRouting
{
    public bool PreferFallback { get; set; }
}
