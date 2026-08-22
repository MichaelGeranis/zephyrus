using Zephyrus.Core.Enums;

namespace Zephyrus.Application.Exceptions;

/// <summary>
/// Thrown when a caller attempts an approval their roles do not permit, or
/// attempts one without being authenticated at all.
/// </summary>
public sealed class UnauthorizedApprovalException : Exception
{
    public UnauthorizedApprovalException(string message) : base(message)
    {
    }

    public static UnauthorizedApprovalException NotAuthenticated(ArtifactType type)
        => new($"Approving a {type} artifact requires an authenticated team member.");

    public static UnauthorizedApprovalException WrongRole(
        ArtifactType type,
        IEnumerable<TeamRole> required,
        IEnumerable<TeamRole> held)
    {
        var requiredText = string.Join(" or ", required);
        var heldText = held.Any() ? string.Join(", ", held) : "none";

        return new UnauthorizedApprovalException(
            $"Approving a {type} artifact requires the {requiredText} role. Caller holds: {heldText}.");
    }
}
