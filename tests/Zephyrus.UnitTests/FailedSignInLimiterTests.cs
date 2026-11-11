using Microsoft.Extensions.Options;
using Zephyrus.Api.Authentication;
using Zephyrus.UnitTests.Jobs;

namespace Zephyrus.UnitTests;

public class FailedSignInLimiterTests
{
    private readonly ManualTimeProvider _clock = new(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc));

    private FailedSignInLimiter CreateLimiter(int limit = 3, int windowMinutes = 15) =>
        new(Options.Create(new FailedSignInOptions { Limit = limit, Window = TimeSpan.FromMinutes(windowMinutes) }), _clock);

    private static void Fail(FailedSignInLimiter limiter, string client, int times)
    {
        for (var i = 0; i < times; i++)
            limiter.RecordFailure(client);
    }

    [Fact]
    public void IsBlocked_WhenClientHasNoFailures_ShouldBeFalse()
    {
        var limiter = CreateLimiter();

        Assert.False(limiter.IsBlocked("10.0.0.1", out _));
    }

    [Fact]
    public void IsBlocked_WhenFailuresAreBelowTheLimit_ShouldBeFalse()
    {
        var limiter = CreateLimiter(limit: 3);
        Fail(limiter, "10.0.0.1", 2);

        Assert.False(limiter.IsBlocked("10.0.0.1", out _));
    }

    [Fact]
    public void IsBlocked_WhenFailuresReachTheLimit_ShouldBeTrueUntilTheWindowEnds()
    {
        var limiter = CreateLimiter(limit: 3, windowMinutes: 15);
        Fail(limiter, "10.0.0.1", 3);
        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.True(limiter.IsBlocked("10.0.0.1", out var retryAfter));
        Assert.Equal(TimeSpan.FromMinutes(10), retryAfter);
    }

    [Fact]
    public void IsBlocked_WhenTheWindowHasEnded_ShouldBeFalse()
    {
        var limiter = CreateLimiter(limit: 3, windowMinutes: 15);
        Fail(limiter, "10.0.0.1", 3);
        _clock.Advance(TimeSpan.FromMinutes(15));

        Assert.False(limiter.IsBlocked("10.0.0.1", out _));
    }

    [Fact]
    public void RecordFailure_WhenTheWindowHasEnded_ShouldStartCountingAgain()
    {
        var limiter = CreateLimiter(limit: 3, windowMinutes: 15);
        Fail(limiter, "10.0.0.1", 3);
        _clock.Advance(TimeSpan.FromMinutes(16));

        Fail(limiter, "10.0.0.1", 2);

        Assert.False(limiter.IsBlocked("10.0.0.1", out _));
    }

    [Fact]
    public void IsBlocked_WhenAnotherClientFailed_ShouldNotAffectThisClient()
    {
        var limiter = CreateLimiter(limit: 3);
        Fail(limiter, "10.0.0.1", 5);

        Assert.True(limiter.IsBlocked("10.0.0.1", out _));
        Assert.False(limiter.IsBlocked("10.0.0.2", out _));
    }
}
