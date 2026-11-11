using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Pipeline;

/// <summary>
/// Domain rule for <em>who</em> may approve each artifact type, mirroring the
/// approval matrix in BUSINESS.md. <see cref="PipelineStateMachine"/> governs
/// when a transition is legal; this governs who is allowed to trigger it.
/// </summary>
public static class ApprovalAuthority
{
    /// <summary>
    /// Roles permitted to approve each artifact type.
    /// </summary>
    /// <remarks>
    /// A Task artifact lists both PM/EM and Tech Lead. An artifact is approved
    /// exactly once by exactly one person, so either role satisfies the gate —
    /// requiring two distinct sign-offs would need a second approval record.
    /// </remarks>
    private static readonly Dictionary<ArtifactType, TeamRole[]> Authorised = new()
    {
        { ArtifactType.Prd, new[] { TeamRole.PmEm } },
        { ArtifactType.Adr, new[] { TeamRole.TechLead } },
        { ArtifactType.Task, new[] { TeamRole.PmEm, TeamRole.TechLead } },
        { ArtifactType.Pr, new[] { TeamRole.TechLead } },
        { ArtifactType.Test, new[] { TeamRole.Qa } },
        { ArtifactType.Workflow, new[] { TeamRole.TechLead } },
    };

    /// <summary>
    /// Returns the roles allowed to approve <paramref name="type"/>.
    /// An artifact type with no entry cannot be approved by anyone.
    /// </summary>
    public static IReadOnlyCollection<TeamRole> RolesFor(ArtifactType type)
        => Authorised.TryGetValue(type, out var roles) ? roles : Array.Empty<TeamRole>();

    /// <summary>
    /// Whether any of <paramref name="heldRoles"/> may approve <paramref name="type"/>.
    /// </summary>
    public static bool CanApprove(ArtifactType type, IEnumerable<TeamRole> heldRoles)
    {
        var allowed = RolesFor(type);
        return allowed.Count > 0 && heldRoles.Any(role => allowed.Contains(role));
    }
}
