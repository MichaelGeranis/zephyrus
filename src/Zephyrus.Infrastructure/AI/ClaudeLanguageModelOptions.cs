namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// Settings for the Claude API provider.
/// </summary>
public sealed class ClaudeLanguageModelOptions
{
    public const string SectionName = "Claude";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>
    /// Output ceiling per call. Thinking is always on for current Claude models
    /// and counts toward it, so this is set well above the expected answer size.
    /// </summary>
    public int MaxTokens { get; set; } = 16000;

    /// <summary>
    /// <c>low</c>, <c>medium</c>, <c>high</c>, <c>xhigh</c> or <c>max</c>.
    /// Claude Opus 5.5 defaults to <c>medium</c> when this is empty.
    /// </summary>
    public string? Effort { get; set; } = "high";

    /// <summary>
    /// When Claude's safety classifiers decline a request, let the API re-run
    /// it on Anthropic's recommended fallback model instead of failing.
    /// Turn off for models that do not support server-side fallbacks.
    /// </summary>
    public bool ServerSideFallback { get; set; } = true;
}
