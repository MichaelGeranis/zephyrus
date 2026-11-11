using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.UnitTests;

/// <summary>
/// Configurable <see cref="IUserContext"/> for tests. Defaults to an
/// authenticated member holding every role.
/// </summary>
public sealed class FakeUserContext : IUserContext
{
    public bool IsAuthenticated { get; set; } = true;

    public string? UserId { get; set; } = "tl@test.com";

    public IReadOnlyCollection<TeamRole> Roles { get; set; } =
        new[] { TeamRole.PmEm, TeamRole.TechLead, TeamRole.Qa };
}
