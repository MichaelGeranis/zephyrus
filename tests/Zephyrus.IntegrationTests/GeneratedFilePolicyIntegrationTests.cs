using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zephyrus.Application.Managers;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Exceptions;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.IntegrationTests;

/// <summary>
/// The Code Agent's output names the files it writes, so a model steered by a
/// malicious issue could target CI. Uses its own fixture because it changes
/// the fake model's Code Agent answer.
/// </summary>
public class GeneratedFilePolicyIntegrationTests : IClassFixture<PipelineFixture>
{
    private readonly PipelineFixture _fixture;

    public GeneratedFilePolicyIntegrationTests(PipelineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApproveTasks_WhenCodeAgentTargetsAWorkflowFile_ShouldCommitNothingAndExplainWhy()
    {
        _fixture.LanguageModel.CodeAgentResponse = """
            {
              "files": [
                { "path": "src/Feature.cs", "content": "namespace Feature;" },
                { "path": ".github/workflows/exfiltrate.yml", "content": "on: push" }
              ]
            }
            """;

        Guid featureId;
        using (var scope = _fixture.CreateScope())
        {
            var project = await scope.ServiceProvider.GetRequiredService<ProjectManager>().CreateAsync(
                "Policy Project", "test", "config: test", "test-owner/policy-repo", "fake-token", Ct);
            featureId = (await scope.ServiceProvider.GetRequiredService<FeatureManager>()
                .CreateAsync(project.Id, "Add a feature", Ct)).Id;
        }

        using (var scope = _fixture.CreateScope())
            await scope.ServiceProvider.GetRequiredService<InvokePrdAgentUseCase>().ExecuteAsync(featureId, ct: Ct);

        // Approving the PRD and ADR runs the Architect and Task Agents inline.
        await Approve(featureId, ArtifactType.Prd);
        await Approve(featureId, ArtifactType.Adr);

        // Approving the tasks runs the Code Agent inline, so its refusal surfaces here.
        var error = await Assert.ThrowsAsync<GeneratedFileRejectedException>(
            () => Approve(featureId, ArtifactType.Task));

        Assert.Contains(".github/workflows/exfiltrate.yml", error.Message);
        Assert.DoesNotContain(_fixture.CodeHost.Files.Keys,
            file => file.Repo == "test-owner/policy-repo" && file.Branch.StartsWith("feature/", StringComparison.Ordinal));
    }

    private async Task Approve(Guid featureId, ArtifactType type)
    {
        Guid artifactId;
        using (var scope = _fixture.CreateScope())
        {
            var artifact = await scope.ServiceProvider.GetRequiredService<IArtifactRepository>()
                .GetByFeatureIdAndTypeAsync(featureId, type, Ct);
            artifactId = artifact!.Id;
        }

        using (var scope = _fixture.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>().ExecuteAsync(featureId, artifactId, Ct);
    }
}
