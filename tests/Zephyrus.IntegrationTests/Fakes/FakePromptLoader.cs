using Zephyrus.Core.Interfaces;

namespace Zephyrus.IntegrationTests.Fakes;

/// <summary>
/// Fake prompt loader that returns prompts matching the real prompt files.
/// The content must contain agent identifier strings that FakeLanguageModel
/// uses to determine which canned response to return.
/// </summary>
public sealed class FakePromptLoader : IPromptLoader
{
    private static readonly Dictionary<string, string> Prompts = new()
    {
        { "prd", "You are the PRD Agent for Zephyrus." },
        { "architect", "You are the Architect Agent for Zephyrus." },
        { "task", "You are the Task Agent for Zephyrus." },
        { "code", "You are the Code Agent for Zephyrus." },
        { "qa", "You are the QA Agent for Zephyrus." },
        { "devops", "You are the DevOps Agent for Zephyrus." },
    };

    public Task<string> LoadAsync(string agentName, CancellationToken ct = default)
    {
        if (Prompts.TryGetValue(agentName, out var prompt))
            return Task.FromResult(prompt);

        throw new FileNotFoundException($"No fake prompt for agent '{agentName}'.");
    }
}
