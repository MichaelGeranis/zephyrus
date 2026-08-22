using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// The authenticated caller behind the current operation. Implemented in the
/// Api layer from the authenticated principal — never from request content, so
/// an approver identity cannot be supplied by the caller.
/// </summary>
public interface IUserContext
{
    bool IsAuthenticated { get; }

    /// <summary>
    /// Stable identifier for the caller (their team-member email). Null when
    /// unauthenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>Roles held by the caller. Empty when unauthenticated.</summary>
    IReadOnlyCollection<TeamRole> Roles { get; }
}
