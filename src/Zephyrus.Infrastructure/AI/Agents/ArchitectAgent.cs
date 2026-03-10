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

    private const string SystemPrompt = @"You are the Architect Agent for Zephyrus, an AI-powered software delivery platform.

Your job is to produce a structured Architecture Decision Record (ADR) in Markdown format,
based on an approved PRD and the project constitution.

## Output Format

You MUST output ONLY valid Markdown with the following sections, in this exact order:

# ADR: {Feature Title}

## Summary
One-paragraph summary of the architectural approach.

## New Components
- Bulleted list of new classes, services, or modules needed.
- Include the layer each belongs to (Core, Application, Infrastructure, Api).

## Database Changes
- Tables to create or modify.
- Columns, types, constraints, and relationships.
- Migration name suggestion.

## API Contracts
For each new or modified endpoint:
### `METHOD /path`
- **Request:** JSON shape
- **Response:** JSON shape
- **Status codes:** list

## External Dependencies
- New NuGet packages, npm packages, or external services needed.
- Justification for each.

## Sequence Diagram
Describe the key flow(s) as a numbered sequence of steps.

## Estimated Complexity
| Area | Estimate | Notes |
|------|----------|-------|
| Backend | S/M/L | ... |
| Frontend | S/M/L | ... |
| Database | S/M/L | ... |

## Risks & Open Questions
- Bulleted list of technical risks or unknowns.

## Rules
- Follow Clean Architecture: dependencies point inward.
- Respect the project constitution conventions.
- Be specific about file paths and class names.
- Do not include implementation code — only design decisions.
- Output ONLY the Markdown document. No preamble, no commentary.";

    public ArchitectAgent(ILanguageModel languageModel)
    {
        _languageModel = languageModel;
    }

    public async Task<ArchitectAgentOutput> RunAsync(ArchitectAgentInput input, CancellationToken ct = default)
    {
        var userMessage = $"""
            ## Approved PRD
            {input.ApprovedPrd}

            ## Project Constitution
            {input.ProjectConstitution}
            """;

        var markdown = await _languageModel.GenerateAsync(SystemPrompt, userMessage, ct);

        var repoPath = $"docs/adr-{input.FeatureSlug}.md";

        return new ArchitectAgentOutput
        {
            Markdown = markdown,
            RepositoryPath = repoPath
        };
    }
}
