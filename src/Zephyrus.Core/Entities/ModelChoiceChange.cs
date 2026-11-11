using Zephyrus.Core.Jobs;

namespace Zephyrus.Core.Entities;

/// <summary>
/// One change to the models a project uses for a pipeline stage: who chose
/// which models, and when. Changes are append-only; a stage's current choice
/// is its latest change (see <see cref="ModelChoices.Current"/>).
/// </summary>
public class ModelChoiceChange
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }

    /// <summary>The pipeline stage, named after the agent that runs it.</summary>
    public AgentJobKind Stage { get; private set; }

    /// <summary>The primary model, as <c>provider:model</c>; null when the stage went back to the default.</summary>
    public string? Primary { get; private set; }

    /// <summary>The fallback model; null to keep the default's, <c>none</c> for no fallback.</summary>
    public string? Fallback { get; private set; }

    public string ChangedBy { get; private set; } = string.Empty;
    public DateTime ChangedAt { get; private set; }

    /// <summary>Whether this change returned the stage to the configured default.</summary>
    public bool IsReset => Primary is null;

    private ModelChoiceChange() { }

    public static ModelChoiceChange Choose(Guid projectId, AgentJobKind stage, ModelChoice choice, string changedBy, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Stage = stage,
            Primary = choice.Primary,
            Fallback = choice.Fallback,
            ChangedBy = changedBy,
            ChangedAt = now
        };

    public static ModelChoiceChange Reset(Guid projectId, AgentJobKind stage, string changedBy, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Stage = stage,
            ChangedBy = changedBy,
            ChangedAt = now
        };
}

/// <summary>A project's choice of models for one stage.</summary>
/// <param name="Primary">Answers first, as <c>provider:model</c>.</param>
/// <param name="Fallback">Takes over when the primary fails; null keeps the default's, <c>none</c> turns it off.</param>
public sealed record ModelChoice(string Primary, string? Fallback);

public static class ModelChoices
{
    /// <summary>Each stage's current choice: its latest change, unless that was a reset.</summary>
    public static IReadOnlyDictionary<AgentJobKind, ModelChoice> Current(IEnumerable<ModelChoiceChange> changes) =>
        changes
            .GroupBy(c => c.Stage)
            .Select(g => g.OrderBy(c => c.ChangedAt).Last())
            .Where(c => !c.IsReset)
            .ToDictionary(c => c.Stage, c => new ModelChoice(c.Primary!, c.Fallback));
}
