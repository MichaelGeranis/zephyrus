using System.Text.Json;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.AI.Agents;

/// <summary>
/// DevOps Agent — generates a GitHub Actions CI/CD workflow file.
/// Reads the Project Constitution to determine stack, conventions, and
/// deployment target, then produces a complete workflow YAML.
/// </summary>
public sealed class DevOpsAgent : IAgent<DevOpsAgentInput, DevOpsAgentOutput>
{
    private readonly ILanguageModel _languageModel;

    private const string SystemPrompt = @"You are the DevOps Agent for Zephyrus, an AI-powered software delivery platform.

Your job is to generate a GitHub Actions CI/CD workflow file that builds, tests, and deploys the project.

## Output Format

You MUST output ONLY valid JSON (no markdown fences, no commentary) with this exact structure:

{
  ""workflow_yaml"": ""name: Deploy\n\non:\n  push:\n    branches: [main]\n\njobs:\n  build:\n    runs-on: ubuntu-latest\n    steps:\n      - uses: actions/checkout@v4\n      ...""
}

## Rules

- Generate a complete, valid GitHub Actions workflow YAML.
- The workflow must include: build, test, and deploy stages.
- Match the project's stack: use dotnet for .NET, npm for Node.js, etc.
- Include environment variable references using GitHub Secrets syntax (${{ secrets.NAME }}).
- Use the deployment target specified in the Project Constitution.
- Pin action versions to specific major versions (e.g., actions/checkout@v4).
- Include caching for dependencies (NuGet, npm) to speed up builds.
- The deploy step should only run on the main branch.
- Output ONLY the JSON. No preamble, no markdown fences, no commentary.";

    public DevOpsAgent(ILanguageModel languageModel)
    {
        _languageModel = languageModel;
    }

    public async Task<DevOpsAgentOutput> RunAsync(DevOpsAgentInput input, CancellationToken ct = default)
    {
        var userMessage = $"""
            ## Repository: {input.RepositorySlug}

            ## Deployment Target
            {input.DeploymentTarget}

            ## Project Constitution
            {input.ProjectConstitution}
            """;

        var json = await _languageModel.GenerateAsync(SystemPrompt, userMessage, ct);

        var workflowYaml = ParseOutput(json);

        var repoPath = ".github/workflows/deploy.yml";

        return new DevOpsAgentOutput
        {
            WorkflowYaml = workflowYaml,
            RepositoryPath = repoPath
        };
    }

    private static string ParseOutput(string json)
    {
        // Strip markdown code fences if the LLM wraps the output
        json = json.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline >= 0)
                json = json[(firstNewline + 1)..];
            if (json.EndsWith("```"))
                json = json[..^3];
            json = json.Trim();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return root.GetProperty("workflow_yaml").GetString()
            ?? throw new InvalidOperationException("Workflow YAML is required.");
    }
}
