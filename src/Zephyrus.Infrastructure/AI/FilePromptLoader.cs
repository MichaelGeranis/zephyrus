using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// Loads agent system prompts from the /prompts/ directory.
/// </summary>
public sealed class FilePromptLoader : IPromptLoader
{
    private readonly string _promptsDirectory;

    public FilePromptLoader(string promptsDirectory)
    {
        _promptsDirectory = promptsDirectory;
    }

    public async Task<string> LoadAsync(string agentName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_promptsDirectory, $"{agentName}.md");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Prompt file not found for agent '{agentName}' at '{filePath}'.");
        }

        return await File.ReadAllTextAsync(filePath, ct);
    }
}
