namespace Zephyrus.Core.Jobs;

/// <summary>
/// A unit of background work: invoke one agent for one feature.
/// Carries only identifiers — the job is re-resolved against the database
/// when it runs, so agents stay stateless and jobs stay safe to retry.
/// </summary>
/// <param name="ForceRerun">
/// A person asked to re-run this step: run it even if the feature has moved
/// past the step's usual status, replacing the step's previous artifact.
/// </param>
public sealed record AgentJob(Guid FeatureId, AgentJobKind Kind, bool ForceRerun = false);
