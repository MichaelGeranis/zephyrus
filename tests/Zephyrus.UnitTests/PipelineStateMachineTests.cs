using Xunit;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Exceptions;
using Zephyrus.Core.Pipeline;

namespace Zephyrus.UnitTests;

public class PipelineStateMachineTests
{
    // --- Next(): valid transitions ---

    [Fact]
    public void Next_WhenIdeation_ShouldReturnPrdPending()
    {
        var next = PipelineStateMachine.Next(FeatureStatus.Ideation);
        Assert.Equal(FeatureStatus.PrdPending, next);
    }

    [Fact]
    public void Next_WhenPrdPending_ShouldReturnPrdApproved()
    {
        var next = PipelineStateMachine.Next(FeatureStatus.PrdPending);
        Assert.Equal(FeatureStatus.PrdApproved, next);
    }

    [Fact]
    public void Next_WhenPrdApproved_ShouldReturnArchPending()
    {
        var next = PipelineStateMachine.Next(FeatureStatus.PrdApproved);
        Assert.Equal(FeatureStatus.ArchPending, next);
    }

    [Fact]
    public void Next_WhenArchPending_ShouldReturnArchApproved()
    {
        var next = PipelineStateMachine.Next(FeatureStatus.ArchPending);
        Assert.Equal(FeatureStatus.ArchApproved, next);
    }

    [Fact]
    public void Next_WhenArchApproved_ShouldReturnTasksPending()
    {
        var next = PipelineStateMachine.Next(FeatureStatus.ArchApproved);
        Assert.Equal(FeatureStatus.TasksPending, next);
    }

    [Fact]
    public void Next_WhenTasksPending_ShouldReturnTasksApproved()
    {
        var next = PipelineStateMachine.Next(FeatureStatus.TasksPending);
        Assert.Equal(FeatureStatus.TasksApproved, next);
    }

    [Fact]
    public void Next_WhenTasksApproved_ShouldReturnCoding()
    {
        var next = PipelineStateMachine.Next(FeatureStatus.TasksApproved);
        Assert.Equal(FeatureStatus.Coding, next);
    }

    [Fact]
    public void Next_WhenCoding_ShouldReturnQaPending()
    {
        var next = PipelineStateMachine.Next(FeatureStatus.Coding);
        Assert.Equal(FeatureStatus.QaPending, next);
    }

    [Fact]
    public void Next_WhenQaPending_ShouldReturnQaApproved()
    {
        var next = PipelineStateMachine.Next(FeatureStatus.QaPending);
        Assert.Equal(FeatureStatus.QaApproved, next);
    }

    [Fact]
    public void Next_WhenQaApproved_ShouldReturnDeployed()
    {
        var next = PipelineStateMachine.Next(FeatureStatus.QaApproved);
        Assert.Equal(FeatureStatus.Deployed, next);
    }

    // --- Next(): terminal state throws ---

    [Fact]
    public void Next_WhenDeployed_ShouldThrowInvalidTransitionException()
    {
        var ex = Assert.Throws<InvalidTransitionException>(
            () => PipelineStateMachine.Next(FeatureStatus.Deployed));

        Assert.Equal(FeatureStatus.Deployed, ex.FromStatus);
    }

    // --- CanTransition(): valid transitions ---

    [Theory]
    [InlineData(FeatureStatus.Ideation, FeatureStatus.PrdPending)]
    [InlineData(FeatureStatus.PrdPending, FeatureStatus.PrdApproved)]
    [InlineData(FeatureStatus.PrdApproved, FeatureStatus.ArchPending)]
    [InlineData(FeatureStatus.ArchPending, FeatureStatus.ArchApproved)]
    [InlineData(FeatureStatus.ArchApproved, FeatureStatus.TasksPending)]
    [InlineData(FeatureStatus.TasksPending, FeatureStatus.TasksApproved)]
    [InlineData(FeatureStatus.TasksApproved, FeatureStatus.Coding)]
    [InlineData(FeatureStatus.Coding, FeatureStatus.QaPending)]
    [InlineData(FeatureStatus.QaPending, FeatureStatus.QaApproved)]
    [InlineData(FeatureStatus.QaApproved, FeatureStatus.Deployed)]
    public void CanTransition_WhenValidTransition_ShouldReturnTrue(
        FeatureStatus from, FeatureStatus to)
    {
        Assert.True(PipelineStateMachine.CanTransition(from, to));
    }

    // --- CanTransition(): backward transitions ---

    [Theory]
    [InlineData(FeatureStatus.PrdPending, FeatureStatus.Ideation)]
    [InlineData(FeatureStatus.PrdApproved, FeatureStatus.PrdPending)]
    [InlineData(FeatureStatus.ArchApproved, FeatureStatus.ArchPending)]
    [InlineData(FeatureStatus.Deployed, FeatureStatus.QaApproved)]
    public void CanTransition_WhenBackwardTransition_ShouldReturnFalse(
        FeatureStatus from, FeatureStatus to)
    {
        Assert.False(PipelineStateMachine.CanTransition(from, to));
    }

    // --- CanTransition(): skipping steps ---

    [Theory]
    [InlineData(FeatureStatus.Ideation, FeatureStatus.PrdApproved)]
    [InlineData(FeatureStatus.Ideation, FeatureStatus.Deployed)]
    [InlineData(FeatureStatus.PrdPending, FeatureStatus.ArchPending)]
    [InlineData(FeatureStatus.TasksApproved, FeatureStatus.QaPending)]
    public void CanTransition_WhenSkippingSteps_ShouldReturnFalse(
        FeatureStatus from, FeatureStatus to)
    {
        Assert.False(PipelineStateMachine.CanTransition(from, to));
    }

    // --- CanTransition(): self-transition ---

    [Theory]
    [InlineData(FeatureStatus.Ideation)]
    [InlineData(FeatureStatus.PrdPending)]
    [InlineData(FeatureStatus.Deployed)]
    public void CanTransition_WhenSameStatus_ShouldReturnFalse(FeatureStatus status)
    {
        Assert.False(PipelineStateMachine.CanTransition(status, status));
    }

    // --- CanTransition(): terminal state ---

    [Theory]
    [InlineData(FeatureStatus.PrdPending)]
    [InlineData(FeatureStatus.Ideation)]
    [InlineData(FeatureStatus.Coding)]
    public void CanTransition_WhenFromDeployed_ShouldAlwaysReturnFalse(FeatureStatus to)
    {
        Assert.False(PipelineStateMachine.CanTransition(FeatureStatus.Deployed, to));
    }
}
