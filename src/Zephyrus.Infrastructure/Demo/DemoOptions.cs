namespace Zephyrus.Infrastructure.Demo;

/// <summary>
/// Demo mode replaces the Claude API and GitHub with local stand-ins so the
/// app can be tried with no API keys. Never enable it on a shared deployment.
/// </summary>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    public bool Enabled { get; set; }

    /// <summary>Folder the demo code host writes repositories to.</summary>
    public string RepositoryRoot { get; set; } = "demo-repos";
}
