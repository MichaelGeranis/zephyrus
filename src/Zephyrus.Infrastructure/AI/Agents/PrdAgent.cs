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

    private const string SystemPrompt = @"You are the PRD Agent for Zephyrus, an AI-powered software delivery platform.

Your job is to produce a structured Product Requirements Document (PRD) in Markdown format.

## Output Format

You MUST output ONLY valid Markdown with the following sections, in this exact order:

# PRD: {Feature Title}

## Problem Statement
Describe the problem this feature solves.

## Target Users
Who benefits from this feature and why.

## Goals
- Bulleted list of what this feature should achieve.

## Non-Goals
- Bulleted list of what is explicitly out of scope.

## User Stories
Use Given / When / Then format for each story:

### Story 1: {Title}
- **Given** {precondition}
- **When** {action}
- **Then** {expected outcome}

(Repeat for each user story)

## Acceptance Criteria
- [ ] Checkbox list of verifiable criteria that must be met.

## Open Questions
- Bulleted list of unresolved questions or decisions needed.

## Rules
- Be specific and actionable — avoid vague language.
- Acceptance criteria must be testable.
- User stories must follow Given / When / Then format strictly.
- Do not include implementation details — those belong in the Architecture phase.
- Output ONLY the Markdown document. No preamble, no commentary.";

    public PrdAgent(ILanguageModel languageModel)
    {
        _languageModel = languageModel;
    }

    public async Task<PrdAgentOutput> RunAsync(PrdAgentInput input, CancellationToken ct = default)
    {
        var userMessage = $"""
            ## Feature Prompt
            {input.FeaturePrompt}

            ## Project Constitution
            {input.ProjectConstitution}
            """;

        var markdown = await _languageModel.GenerateAsync(SystemPrompt, userMessage, ct);

        var repoPath = $"docs/prd-{input.FeatureSlug}.md";

        return new PrdAgentOutput
        {
            Markdown = markdown,
            RepositoryPath = repoPath
        };
    }
}
