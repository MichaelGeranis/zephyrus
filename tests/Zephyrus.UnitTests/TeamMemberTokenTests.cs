using System.Security.Cryptography;
using System.Text;
using Zephyrus.Api.Authentication;
using Zephyrus.Core.Entities;

namespace Zephyrus.UnitTests;

public class TeamMemberTokenTests
{
    private static string Sha256Hex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [Fact]
    public void Matches_WhenConfiguredWithHash_ShouldAcceptTheTokenBehindIt()
    {
        var member = new TeamMemberOptions { TokenHash = Sha256Hex("correct horse") };

        Assert.True(member.Matches("correct horse"));
        Assert.False(member.Matches("wrong horse"));
    }

    [Fact]
    public void Matches_WhenConfiguredWithHash_ShouldNotAcceptTheHashItself()
    {
        var hash = Sha256Hex("correct horse");
        var member = new TeamMemberOptions { TokenHash = hash };

        Assert.False(member.Matches(hash));
    }

    [Fact]
    public void Matches_WhenHashIsUppercase_ShouldStillMatch()
    {
        var member = new TeamMemberOptions { TokenHash = Sha256Hex("correct horse").ToUpperInvariant() };

        Assert.True(member.Matches("correct horse"));
    }

    [Fact]
    public void Matches_WhenBothAreSet_ShouldUseTheHash()
    {
        var member = new TeamMemberOptions { Token = "plain", TokenHash = Sha256Hex("hashed") };

        Assert.True(member.Matches("hashed"));
        Assert.False(member.Matches("plain"));
    }

    [Fact]
    public void Matches_WhenConfiguredWithPlainToken_ShouldStillWork()
    {
        var member = new TeamMemberOptions { Token = "plain" };

        Assert.True(member.Matches("plain"));
        Assert.False(member.Matches("plain2"));
    }

    [Theory]
    [InlineData("not-hex")]
    [InlineData("abcd")]
    public void Matches_WhenHashIsMalformed_ShouldNeverMatch(string hash)
    {
        var member = new TeamMemberOptions { TokenHash = hash };

        Assert.False(member.HasValidTokenHash);
        Assert.False(member.Matches("anything"));
    }

    [Fact]
    public void Matches_WhenNothingIsConfigured_ShouldNeverMatch()
    {
        Assert.False(new TeamMemberOptions().Matches(""));
    }

    [Fact]
    public void RemoveContent_ShouldKeepTheRunButNotWhatItReadOrWrote()
    {
        var run = AgentInvocation.Create(Guid.NewGuid(), "prd", "system prompt", "constitution and idea", "the PRD", 100);
        var at = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

        run.RemoveContent(at);

        Assert.Equal(AgentInvocation.RemovedContent, run.UserMessage);
        Assert.Equal(AgentInvocation.RemovedContent, run.Response);
        Assert.Equal("system prompt", run.SystemPrompt);
        Assert.Equal(AgentInvocation.VersionOf("system prompt"), run.PromptVersion);
        Assert.Equal(at, run.ContentRemovedAt);
    }

    [Fact]
    public void RemoveContent_WhenAlreadyRemoved_ShouldKeepTheFirstDate()
    {
        var run = AgentInvocation.Create(Guid.NewGuid(), "prd", "s", "u", "r", 100);
        var first = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

        run.RemoveContent(first);
        run.RemoveContent(first.AddDays(1));

        Assert.Equal(first, run.ContentRemovedAt);
    }
}
