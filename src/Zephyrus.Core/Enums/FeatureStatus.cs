namespace Zephyrus.Core.Enums;

/// <summary>
/// Represents the current stage of a Feature in the delivery pipeline.
/// </summary>
public enum FeatureStatus
{
    Ideation,
    PrdPending,
    PrdApproved,
    ArchPending,
    ArchApproved,
    TasksPending,
    TasksApproved,
    Coding,
    QaPending,
    QaApproved,
    Deployed
}
