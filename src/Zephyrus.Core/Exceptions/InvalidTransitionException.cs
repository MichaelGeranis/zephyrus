using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Exceptions;

/// <summary>
/// Thrown when a pipeline state transition is not valid.
/// </summary>
public class InvalidTransitionException : Exception
{
    public FeatureStatus FromStatus { get; }
    public FeatureStatus ToStatus { get; }

    public InvalidTransitionException(FeatureStatus from, FeatureStatus to)
        : base($"Invalid pipeline transition from {from} to {to}.")
    {
        FromStatus = from;
        ToStatus = to;
    }
}
