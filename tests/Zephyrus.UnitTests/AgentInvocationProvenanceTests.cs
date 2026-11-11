using Zephyrus.Core.Entities;

namespace Zephyrus.UnitTests;

public class AgentInvocationProvenanceTests
{
    [Fact]
    public void VersionOf_ShouldBeTheFirst12HexCharactersOfTheSha256()
    {
        // SHA-256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
        Assert.Equal("ba7816bf8f01", AgentInvocation.VersionOf("abc"));
    }

    [Fact]
    public void Create_ShouldRecordThePromptVersionAndTheArtifactProduced()
    {
        var artifactId = Guid.NewGuid();

        var run = AgentInvocation.Create(Guid.NewGuid(), "prd", "You are the PRD Agent.", "idea", "# PRD", 120, artifactId);

        Assert.Equal(AgentInvocation.VersionOf("You are the PRD Agent."), run.PromptVersion);
        Assert.Equal(artifactId, run.ArtifactId);
    }

    [Fact]
    public void VersionOf_WhenThePromptChanges_ShouldChange()
    {
        Assert.NotEqual(AgentInvocation.VersionOf("You are the PRD Agent."), AgentInvocation.VersionOf("You are the PRD Agent. Be brief."));
    }
}
