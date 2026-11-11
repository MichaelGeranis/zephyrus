namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// Settings for a model served through an OpenAI-compatible API: Ollama,
/// LM Studio, vLLM or llama.cpp's server.
/// </summary>
public sealed class LocalModelOptions
{
    public const string SectionName = "LocalModel";

    /// <summary>Base URL including the version segment, e.g. <c>http://localhost:11434/v1</c>.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";

    public string Model { get; set; } = "llama3.1:8b";

    /// <summary>Only needed for servers that require one; Ollama does not.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public int MaxTokens { get; set; } = 8192;

    /// <summary>Local models on modest hardware are slow; agent prompts are long.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
}
