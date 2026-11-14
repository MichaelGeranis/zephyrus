using System.Security.Cryptography;
using System.Text;

namespace Zephyrus.Core.Entities;

public class AgentInvocation
{
    public Guid Id { get; private set; }
    public Guid FeatureId { get; private set; }
    public string AgentName { get; private set; } = string.Empty;
    public string SystemPrompt { get; private set; } = string.Empty;
    public string UserMessage { get; private set; } = string.Empty;
    public string Response { get; private set; } = string.Empty;
    public DateTime InvokedAt { get; private set; }
    public int DurationMs { get; private set; }

    /// <summary>The model that actually answered, which may be a fallback.</summary>
    public string? Model { get; private set; }
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }

    /// <summary>
    /// Estimated at the time of the run from the configured price table, so a
    /// later price change does not rewrite history. Null when the model's price
    /// is unknown.
    /// </summary>
    public decimal? EstimatedCostUsd { get; private set; }

    /// <summary>
    /// Identifies the agent's prompt: the first 12 hex characters of the
    /// SHA-256 of the system prompt. Runs with the same value used the same
    /// prompt file, so a change of prompt shows up in the audit trail.
    /// </summary>
    public string PromptVersion { get; private set; } = string.Empty;

    /// <summary>
    /// The artifact this run produced or contributed to. Kept when a re-run
    /// replaces the artifact, so the history of what produced what survives.
    /// </summary>
    public Guid? ArtifactId { get; private set; }

    /// <summary>
    /// When the run's input and output were removed under the content retention
    /// period, or null while they are kept. The run itself stays: who ran what,
    /// with which model and prompt version, at what cost.
    /// </summary>
    public DateTime? ContentRemovedAt { get; private set; }

    public Feature Feature { get; private set; } = null!;

    private AgentInvocation() { }

    public static AgentInvocation Create(
        Guid featureId,
        string agentName,
        string systemPrompt,
        string userMessage,
        string response,
        int durationMs,
        Guid? artifactId = null)
    {
        return new AgentInvocation
        {
            Id = Guid.NewGuid(),
            FeatureId = featureId,
            AgentName = agentName,
            SystemPrompt = systemPrompt,
            UserMessage = userMessage,
            Response = response,
            InvokedAt = DateTime.UtcNow,
            DurationMs = durationMs,
            PromptVersion = VersionOf(systemPrompt),
            ArtifactId = artifactId
        };
    }

    /// <summary>Stands in for the user message and response once they are removed.</summary>
    public const string RemovedContent = "[removed after the retention period]";

    /// <summary>
    /// Removes what the run read and wrote (the project's constitution, artifacts,
    /// issue bodies and code), keeping the record of the run. The system prompt is
    /// the agent's prompt file, not project content, so it stays.
    /// </summary>
    public void RemoveContent(DateTime removedAt)
    {
        if (ContentRemovedAt is not null)
            return;

        UserMessage = RemovedContent;
        Response = RemovedContent;
        ContentRemovedAt = removedAt;
    }

    public static string VersionOf(string systemPrompt) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(systemPrompt)))[..12];

    public void RecordUsage(string model, int inputTokens, int outputTokens, decimal? estimatedCostUsd)
    {
        Model = model;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        EstimatedCostUsd = estimatedCostUsd;
    }
}
