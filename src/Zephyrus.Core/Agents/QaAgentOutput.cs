namespace Zephyrus.Core.Agents;

/// <summary>
/// Output from the QA Agent: test files to commit and a summary report.
/// </summary>
public sealed class QaAgentOutput
{
    public required IReadOnlyList<GeneratedFile> TestFiles { get; init; }
    public required string ReportMarkdown { get; init; }
    public required string RepositoryPath { get; init; }
    public required string SystemPrompt { get; init; }
    public required string UserMessage { get; init; }
    public required string RawResponse { get; init; }
}
