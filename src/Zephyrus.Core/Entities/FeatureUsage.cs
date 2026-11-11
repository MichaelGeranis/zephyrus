namespace Zephyrus.Core.Entities;

/// <summary>
/// What a feature's agent runs used and cost so far.
/// </summary>
/// <param name="EstimatedCostUsd">The sum of the runs' estimates.</param>
/// <param name="CostIsComplete">
/// False when some run used a model without a price, so the sum is a lower bound.
/// </param>
public sealed record FeatureUsage(
    int Runs,
    long InputTokens,
    long OutputTokens,
    decimal EstimatedCostUsd,
    bool CostIsComplete)
{
    public static FeatureUsage From(IEnumerable<AgentInvocation> invocations)
    {
        var measured = invocations.Where(i => i.Model is not null).ToList();

        return new FeatureUsage(
            measured.Count,
            measured.Sum(i => (long)(i.InputTokens ?? 0)),
            measured.Sum(i => (long)(i.OutputTokens ?? 0)),
            measured.Sum(i => i.EstimatedCostUsd ?? 0m),
            measured.All(i => i.EstimatedCostUsd is not null));
    }
}
