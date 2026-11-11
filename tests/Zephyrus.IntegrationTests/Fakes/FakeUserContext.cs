using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.IntegrationTests.Fakes;

/// <summary>
/// Stands in for the authenticated caller when a test drives a use case
/// directly rather than over HTTP. Defaults to a member holding every role,
/// so pipeline tests exercise the pipeline rather than authorisation.
/// </summary>
public sealed class FakeUserContext : IUserContext
{
    public bool IsAuthenticated { get; set; } = true;

    public string? UserId { get; set; } = "pm@company.com";

    public IReadOnlyCollection<TeamRole> Roles { get; set; } =
        new[] { TeamRole.PmEm, TeamRole.TechLead, TeamRole.Qa };
}
