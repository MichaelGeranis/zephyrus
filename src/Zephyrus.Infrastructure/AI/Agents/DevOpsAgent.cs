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
    private readonly IPromptLoader _promptLoader;

    public DevOpsAgent(ILanguageModel languageModel, IPromptLoader promptLoader)
    {
        _languageModel = languageModel;
        _promptLoader = promptLoader;
    }

    public async Task<DevOpsAgentOutput> RunAsync(DevOpsAgentInput input, CancellationToken ct = default)
    {
        var systemPrompt = await _promptLoader.LoadAsync("devops", ct);

        var userMessage = $"""
            ## Repository: {input.RepositorySlug}

            ## Deployment Target
            {input.DeploymentTarget}

            ## Project Constitution
            {input.ProjectConstitution}
            """;

        var json = await _languageModel.GenerateAsync(systemPrompt, userMessage, ct);

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
