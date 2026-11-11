using Zephyrus.Core.Entities;

namespace Zephyrus.UnitTests;

public class FeatureUsageTests
{
    private static AgentInvocation Run(string? model, int input, int output, decimal? cost)
    {
        var run = AgentInvocation.Create(Guid.NewGuid(), "prd", "s", "u", "r", 10);
        if (model is not null)
            run.RecordUsage(model, input, output, cost);
        return run;
    }

    [Fact]
    public void From_WhenRunsArePriced_ShouldSumTokensAndCost()
    {
        var usage = FeatureUsage.From(new[]
        {
            Run("llama3.1:8b", 1000, 200, 0m),
            Run("claude-opus-5-5", 2000, 300, 0.014m),
        });

        Assert.Equal(new FeatureUsage(2, 3000, 500, 0.014m, true), usage);
    }

    [Fact]
    public void From_WhenARunHasNoPrice_ShouldMarkTheCostIncomplete()
    {
        var usage = FeatureUsage.From(new[] { Run("claude-opus-5-5", 10, 10, 0.1m), Run("unpriced-model", 10, 10, null) });

        Assert.False(usage.CostIsComplete);
        Assert.Equal(0.1m, usage.EstimatedCostUsd);
    }

    [Fact]
    public void From_WhenRunsPredateUsageTracking_ShouldIgnoreThem()
    {
        var usage = FeatureUsage.From(new[] { Run(null, 0, 0, null) });

        Assert.Equal(new FeatureUsage(0, 0, 0, 0m, true), usage);
    }
}
