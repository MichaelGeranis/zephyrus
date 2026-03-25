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

    public Feature Feature { get; private set; } = null!;

    private AgentInvocation() { }

    public static AgentInvocation Create(
        Guid featureId,
        string agentName,
        string systemPrompt,
        string userMessage,
        string response,
        int durationMs)
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
            DurationMs = durationMs
        };
    }
}
