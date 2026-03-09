namespace Zephyrus.Core.Entities;

/// <summary>
/// A software project managed by Zephyrus. Contains the Project Constitution
/// and links to the GitHub repository.
/// </summary>
public class Project
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// The Project Constitution stored as JSON. Every agent reads this before acting.
    /// </summary>
    public string Config { get; private set; } = string.Empty;

    /// <summary>
    /// GitHub repository in "owner/repo" format.
    /// </summary>
    public string GitHubRepo { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<Feature> Features => _features.AsReadOnly();
    private readonly List<Feature> _features = new();

    private Project() { }

    public static Project Create(string name, string description, string config, string gitHubRepo)
    {
        return new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Config = config,
            GitHubRepo = gitHubRepo,
            CreatedAt = DateTime.UtcNow
        };
    }
}
