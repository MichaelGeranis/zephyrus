using Zephyrus.Core.Enums;
using Zephyrus.Core.Pipeline;

namespace Zephyrus.UnitTests;

public class ApprovalAuthorityTests
{
    [Theory]
    [InlineData(ArtifactType.Prd, TeamRole.PmEm)]
    [InlineData(ArtifactType.Adr, TeamRole.TechLead)]
    [InlineData(ArtifactType.Task, TeamRole.PmEm)]
    [InlineData(ArtifactType.Task, TeamRole.TechLead)]
    [InlineData(ArtifactType.Pr, TeamRole.TechLead)]
    [InlineData(ArtifactType.Test, TeamRole.Qa)]
    [InlineData(ArtifactType.Workflow, TeamRole.TechLead)]
    public void CanApprove_WhenRoleIsAuthorised_ShouldReturnTrue(ArtifactType type, TeamRole role)
    {
        Assert.True(ApprovalAuthority.CanApprove(type, new[] { role }));
    }

    [Theory]
    [InlineData(ArtifactType.Prd, TeamRole.Qa)]
    [InlineData(ArtifactType.Prd, TeamRole.TechLead)]
    [InlineData(ArtifactType.Adr, TeamRole.PmEm)]
    [InlineData(ArtifactType.Adr, TeamRole.Qa)]
    [InlineData(ArtifactType.Pr, TeamRole.Qa)]
    [InlineData(ArtifactType.Test, TeamRole.PmEm)]
    [InlineData(ArtifactType.Test, TeamRole.TechLead)]
    [InlineData(ArtifactType.Workflow, TeamRole.Qa)]
    public void CanApprove_WhenRoleIsNotAuthorised_ShouldReturnFalse(ArtifactType type, TeamRole role)
    {
        Assert.False(ApprovalAuthority.CanApprove(type, new[] { role }));
    }

    [Fact]
    public void CanApprove_WhenCallerHoldsNoRoles_ShouldReturnFalse()
    {
        Assert.False(ApprovalAuthority.CanApprove(ArtifactType.Prd, Array.Empty<TeamRole>()));
    }

    [Fact]
    public void CanApprove_WhenCallerHoldsSeveralRoles_ShouldAllowAnyAuthorisedOne()
    {
        var roles = new[] { TeamRole.Qa, TeamRole.TechLead };

        Assert.True(ApprovalAuthority.CanApprove(ArtifactType.Adr, roles));
    }

    [Fact]
    public void RolesFor_WhenTaskArtifact_ShouldAllowPmEmAndTechLead()
    {
        var roles = ApprovalAuthority.RolesFor(ArtifactType.Task);

        Assert.Contains(TeamRole.PmEm, roles);
        Assert.Contains(TeamRole.TechLead, roles);
        Assert.DoesNotContain(TeamRole.Qa, roles);
    }
}
