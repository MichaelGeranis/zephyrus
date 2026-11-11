using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Jobs;

namespace Zephyrus.UnitTests;

public class AgentJobRecordTests
{
    private static readonly DateTime T0 = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private static AgentJobRecord NewJob() =>
        AgentJobRecord.Create(new AgentJob(Guid.NewGuid(), AgentJobKind.Architect), T0);

    [Fact]
    public void Create_WhenCalled_ShouldBeQueuedAndDueImmediately()
    {
        var job = NewJob();

        Assert.Equal(AgentJobStatus.Queued, job.Status);
        Assert.Equal(0, job.Attempts);
        Assert.True(job.IsClaimable(T0));
    }

    [Fact]
    public void TryClaim_WhenQueued_ShouldStartAnAttemptWithALease()
    {
        var job = NewJob();
        var stamp = job.ConcurrencyStamp;

        Assert.True(job.TryClaim(T0, Lease));

        Assert.Equal(AgentJobStatus.Running, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Equal(T0 + Lease, job.LeaseExpiresAt);
        Assert.NotEqual(stamp, job.ConcurrencyStamp);
    }

    [Fact]
    public void TryClaim_WhenAnotherWorkerHoldsALiveLease_ShouldRefuse()
    {
        var job = NewJob();
        job.TryClaim(T0, Lease);

        Assert.False(job.TryClaim(T0 + TimeSpan.FromMinutes(5), Lease));
        Assert.Equal(1, job.Attempts);
    }

    [Fact]
    public void TryClaim_WhenLeaseExpiredWithAttemptsLeft_ShouldStartTheNextAttempt()
    {
        var job = NewJob();
        job.TryClaim(T0, Lease);

        Assert.True(job.TryClaim(T0 + Lease + TimeSpan.FromSeconds(1), Lease));
        Assert.Equal(2, job.Attempts);
    }

    [Fact]
    public void TryClaim_WhenLeaseExpiredWithNoAttemptsLeft_ShouldMarkFailed()
    {
        var job = NewJob();
        job.TryClaim(T0, Lease);
        job.Fail("first", T0, RetryDelay);
        job.TryClaim(T0 + RetryDelay, Lease);

        var claimed = job.TryClaim(T0 + RetryDelay + Lease + TimeSpan.FromSeconds(1), Lease);

        Assert.False(claimed);
        Assert.Equal(AgentJobStatus.Failed, job.Status);
        Assert.Contains("interrupted", job.LastError);
    }

    [Fact]
    public void Fail_WhenFirstAttemptFails_ShouldQueueOneRetryAfterTheDelay()
    {
        var job = NewJob();
        job.TryClaim(T0, Lease);

        job.Fail("Claude API returned 529", T0, RetryDelay);

        Assert.Equal(AgentJobStatus.Queued, job.Status);
        Assert.Equal("Claude API returned 529", job.LastError);
        Assert.False(job.IsClaimable(T0 + RetryDelay - TimeSpan.FromSeconds(1)));
        Assert.True(job.IsClaimable(T0 + RetryDelay));
    }

    [Fact]
    public void Fail_WhenRetryAlsoFails_ShouldMarkFailedAndStopRetrying()
    {
        var job = NewJob();
        job.TryClaim(T0, Lease);
        job.Fail("first", T0, RetryDelay);
        job.TryClaim(T0 + RetryDelay, Lease);

        job.Fail("second", T0 + RetryDelay, RetryDelay);

        Assert.Equal(AgentJobStatus.Failed, job.Status);
        Assert.Equal(AgentJobRecord.MaxAttempts, job.Attempts);
        Assert.Equal("second", job.LastError);
        Assert.False(job.IsClaimable(T0 + TimeSpan.FromDays(1)));
    }

    [Fact]
    public void Succeed_WhenRunning_ShouldCompleteAndClearTheError()
    {
        var job = NewJob();
        job.TryClaim(T0, Lease);
        job.Fail("first", T0, RetryDelay);
        job.TryClaim(T0 + RetryDelay, Lease);

        job.Succeed(T0 + RetryDelay);

        Assert.Equal(AgentJobStatus.Succeeded, job.Status);
        Assert.Null(job.LastError);
        Assert.Null(job.LeaseExpiresAt);
        Assert.False(job.IsClaimable(T0 + TimeSpan.FromDays(1)));
    }

    [Fact]
    public void Succeed_WhenNotRunning_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => NewJob().Succeed(T0));
    }

    [Fact]
    public void Fail_WhenErrorIsVeryLong_ShouldTruncateIt()
    {
        var job = NewJob();
        job.TryClaim(T0, Lease);

        job.Fail(new string('x', 10_000), T0, RetryDelay);

        Assert.True(job.LastError!.Length <= 2001);
    }
}
