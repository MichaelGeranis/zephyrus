using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.AI.Agents;

/// <summary>
/// Architect Agent — generates an Architecture Decision Record (ADR) from an
/// approved PRD and project constitution.
/// </summary>
public sealed class ArchitectAgent : IAgent<ArchitectAgentInput, ArchitectAgentOutput>
{
    private readonly ILanguageModel _languageModel;
    private readonly IPromptLoader _promptLoader;

    public ArchitectAgent(ILanguageModel languageModel, IPromptLoader promptLoader)
    {
        _languageModel = languageModel;
        _promptLoader = promptLoader;
    }

    public async Task<ArchitectAgentOutput> RunAsync(ArchitectAgentInput input, CancellationToken ct = default)
    {
        var systemPrompt = await _promptLoader.LoadAsync("architect", ct);

        var codebaseSection = string.IsNullOrWhiteSpace(input.CodebaseMap)
            ? ""
            : $"""

            ## Codebase Map
            {input.CodebaseMap}
            """;

        var userMessage = $"""
            ## Approved PRD
            {input.ApprovedPrd}

            ## Project Constitution
            {input.ProjectConstitution}
            {codebaseSection}
            """;

        var markdown = await _languageModel.GenerateAsync(systemPrompt, userMessage, ct);

        return new ArchitectAgentOutput
        {
            Markdown = markdown,
            SystemPrompt = systemPrompt,
            UserMessage = userMessage,
            RawResponse = markdown
        };
    }
}
