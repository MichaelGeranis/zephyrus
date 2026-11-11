namespace Zephyrus.UnitTests.AI;

public class LanguageModelUsageTrackerTests
{
    [Fact]
    public void Drain_WhenNothingRecorded_ShouldReturnNull()
    {
        Assert.Null(LanguageModelTestSupport.Tracker().Drain());
    }

    [Fact]
    public void Drain_WhenModelIsPriced_ShouldEstimateTheCost()
    {
        var tracker = LanguageModelTestSupport.Tracker(("claude-opus-5-5", 4m, 20m));
        tracker.Record("claude-opus-5-5", 1_000_000, 100_000, free: false);

        var usage = tracker.Drain()!;

        Assert.Equal(6m, usage.EstimatedCostUsd); // $4 input + $2 output
    }

    [Fact]
    public void Drain_WhenApiReturnsADatedModelId_ShouldMatchItsPrefix()
    {
        var tracker = LanguageModelTestSupport.Tracker(("claude-opus-5-5", 4m, 20m));
        tracker.Record("claude-opus-5-5-20261001", 500_000, 0, free: false);

        Assert.Equal(2m, tracker.Drain()!.EstimatedCostUsd);
    }

    [Fact]
    public void Drain_WhenModelPriceIsUnknown_ShouldLeaveTheCostUnknown()
    {
        var tracker = LanguageModelTestSupport.Tracker();
        tracker.Record("claude-some-future-model", 10, 10, free: false);

        Assert.Null(tracker.Drain()!.EstimatedCostUsd);
    }

    [Fact]
    public void Drain_WhenRunFellBackFromALocalModel_ShouldNameBothAndCostOnlyTheFallback()
    {
        var tracker = LanguageModelTestSupport.Tracker(("claude-opus-5-5", 4m, 20m));
        tracker.Record("llama3.1:8b", 2_000, 300, free: true);
        tracker.Record("claude-opus-5-5", 1_000_000, 0, free: false);

        var usage = tracker.Drain()!;

        Assert.Equal("llama3.1:8b → claude-opus-5-5", usage.Model);
        Assert.Equal(1_002_000, usage.InputTokens);
        Assert.Equal(4m, usage.EstimatedCostUsd);
    }

    [Fact]
    public void Drain_WhenCalledTwice_ShouldStartAfreshForTheNextRun()
    {
        var tracker = LanguageModelTestSupport.Tracker();
        tracker.Record("llama3.1:8b", 10, 10, free: true);
        tracker.Drain();

        Assert.Null(tracker.Drain());
    }
}
