namespace Zephyrus.Infrastructure.GitHub;

/// <summary>
/// Configuration options for the GitHub code host integration.
/// </summary>
public sealed class GitHubCodeHostOptions
{
    public const string SectionName = "GitHub";

    /// <summary>
    /// Personal access token or GitHub App token for API authentication.
    /// </summary>
    public required string Token { get; set; }
}
