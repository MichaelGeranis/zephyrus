namespace Zephyrus.Api.Authentication;

/// <summary>
/// The team roster. Zephyrus is built for a small, fixed team, so members are
/// configured rather than self-registered.
/// </summary>
public sealed class TeamOptions
{
    public const string SectionName = "Team";

    public List<TeamMemberOptions> Members { get; set; } = new();
}

/// <summary>
/// One team member and the token that authenticates them.
/// </summary>
public sealed class TeamMemberOptions
{
    /// <summary>Identifies the member; recorded as the approver on artifacts.</summary>
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Bearer token presented by this member. Supply via configuration or an
    /// environment variable — never commit a real one.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Role names, parsed against <c>TeamRole</c>.</summary>
    public List<string> Roles { get; set; } = new();
}
