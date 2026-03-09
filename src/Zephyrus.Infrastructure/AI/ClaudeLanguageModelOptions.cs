namespace Zephyrus.Infrastructure.AI;

public sealed class ClaudeLanguageModelOptions
{
    public const string SectionName = "Claude";

    public required string ApiKey { get; set; }
    public string Model { get; set; } = "claude-sonnet-4-20250514";
    public int MaxTokens { get; set; } = 4096;
}
