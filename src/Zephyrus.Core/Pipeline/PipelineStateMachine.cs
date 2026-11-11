using Zephyrus.Core.Enums;
using Zephyrus.Core.Exceptions;

namespace Zephyrus.Core.Pipeline;

/// <summary>
/// Deterministic state machine governing Feature pipeline transitions.
/// Any transition not in the valid set throws <see cref="InvalidTransitionException"/>.
/// </summary>
public static class PipelineStateMachine
{
    private static readonly Dictionary<FeatureStatus, FeatureStatus> Transitions = new()
    {
        { FeatureStatus.Ideation, FeatureStatus.PrdPending },
        { FeatureStatus.PrdPending, FeatureStatus.PrdApproved },
        { FeatureStatus.PrdApproved, FeatureStatus.ArchPending },
        { FeatureStatus.ArchPending, FeatureStatus.ArchApproved },
        { FeatureStatus.ArchApproved, FeatureStatus.TasksPending },
        { FeatureStatus.TasksPending, FeatureStatus.TasksApproved },
        { FeatureStatus.TasksApproved, FeatureStatus.Coding },
        { FeatureStatus.Coding, FeatureStatus.QaPending },
        { FeatureStatus.QaPending, FeatureStatus.QaApproved },
        { FeatureStatus.QaApproved, FeatureStatus.Deployed },
    };

    /// <summary>
    /// Returns the next valid status for the given current status.
    /// </summary>
    /// <exception cref="InvalidTransitionException">
    /// Thrown when the current status has no valid next state (e.g. already Deployed).
    /// </exception>
    public static FeatureStatus Next(FeatureStatus current)
    {
        if (Transitions.TryGetValue(current, out var next))
        {
            return next;
        }

        throw new InvalidTransitionException(current, current);
    }

    /// <summary>
    /// Validates whether transitioning from <paramref name="from"/> to <paramref name="to"/> is allowed.
    /// </summary>
    public static bool CanTransition(FeatureStatus from, FeatureStatus to)
    {
        return Transitions.TryGetValue(from, out var valid) && valid == to;
    }
}
