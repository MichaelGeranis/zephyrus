using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Pipeline;

/// <summary>
/// Who may change which models a project's stages use. The choice decides
/// what each run costs and whether project content leaves for a hosted model,
/// so it is an architecture decision: the Tech Lead's.
/// </summary>
public static class ModelChoiceAuthority
{
    public static bool CanChoose(IEnumerable<TeamRole> heldRoles) => heldRoles.Contains(TeamRole.TechLead);
}
