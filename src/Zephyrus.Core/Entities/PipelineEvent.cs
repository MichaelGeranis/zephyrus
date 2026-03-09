using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Entities;

/// <summary>
/// An immutable audit record of a pipeline state transition.
/// </summary>
public class PipelineEvent
{
    public Guid Id { get; private set; }
    public Guid FeatureId { get; private set; }
    public FeatureStatus FromStatus { get; private set; }
    public FeatureStatus ToStatus { get; private set; }
    public string TriggeredBy { get; private set; } = string.Empty;
    public DateTime Timestamp { get; private set; }

    public Feature Feature { get; private set; } = null!;

    private PipelineEvent() { }

    public static PipelineEvent Create(
        Guid featureId,
        FeatureStatus fromStatus,
        FeatureStatus toStatus,
        string triggeredBy)
    {
        return new PipelineEvent
        {
            Id = Guid.NewGuid(),
            FeatureId = featureId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            TriggeredBy = triggeredBy,
            Timestamp = DateTime.UtcNow
        };
    }
}
