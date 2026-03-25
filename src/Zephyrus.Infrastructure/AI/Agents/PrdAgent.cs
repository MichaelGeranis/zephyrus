using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.AI.Agents;

/// <summary>
/// PRD Agent — generates a Product Requirements Document from a feature prompt
/// and project constitution, then commits it to the repository.
/// </summary>
public sealed class PrdAgent : IAgent<PrdAgentInput, PrdAgentOutput>
{
    private readonly ILanguageModel _languageModel;
    private readonly IPromptLoader _promptLoader;

    public PrdAgent(ILanguageModel languageModel, IPromptLoader promptLoader)
    {
        _languageModel = languageModel;
        _promptLoader = promptLoader;
    }

    public async Task<PrdAgentOutput> RunAsync(PrdAgentInput input, CancellationToken ct = default)
    {
        var systemPrompt = await _promptLoader.LoadAsync("prd", ct);

        var userMessage = $"""
            ## Feature Prompt
            {input.FeaturePrompt}

            ## Project Constitution
            {input.ProjectConstitution}
            """;

        var markdown = await _languageModel.GenerateAsync(systemPrompt, userMessage, ct);

        var repoPath = $"docs/prd-{input.FeatureSlug}.md";

        return new PrdAgentOutput
        {
            Markdown = markdown,
            RepositoryPath = repoPath,
            SystemPrompt = systemPrompt,
            UserMessage = userMessage,
            RawResponse = markdown
        };
    }
}
