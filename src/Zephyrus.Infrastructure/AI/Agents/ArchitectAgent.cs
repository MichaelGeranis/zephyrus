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

        var userMessage = $"""
            ## Approved PRD
            {input.ApprovedPrd}

            ## Project Constitution
            {input.ProjectConstitution}
            """;

        var markdown = await _languageModel.GenerateAsync(systemPrompt, userMessage, ct);

        var repoPath = $"docs/adr-{input.FeatureSlug}.md";

        return new ArchitectAgentOutput
        {
            Markdown = markdown,
            RepositoryPath = repoPath,
            SystemPrompt = systemPrompt,
            UserMessage = userMessage,
            RawResponse = markdown
        };
    }
}
