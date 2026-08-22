namespace Zephyrus.Core.Jobs;

/// <summary>
/// A unit of background work: invoke one agent for one feature.
/// Carries only identifiers — the job is re-resolved against the database
/// when it runs, so agents stay stateless and jobs stay safe to retry.
/// </summary>
public sealed record AgentJob(Guid FeatureId, AgentJobKind Kind);
