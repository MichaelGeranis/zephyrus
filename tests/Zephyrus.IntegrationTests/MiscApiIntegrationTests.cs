using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Zephyrus.IntegrationTests;

/// <summary>
/// Integration tests for agent invocations, rerun-step, artifact update content, and retry-commit endpoints.
/// </summary>
public class MiscApiIntegrationTests : IClassFixture<ZephyrusApiFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MiscApiIntegrationTests(ZephyrusApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ──────────────────────────────────────────────
    // Agent Invocations
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AgentInvocations_EmptyBeforePrdGeneration()
    {
        var project = await CreateProject("InvocationsEmpty", "org/invocations-empty");
        var feature = await CreateFeature(project.Id, "Empty invocations test");

        var response = await _client.GetAsync($"/api/features/{feature.Id}/agent-invocations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var invocations = await Deserialize<AgentInvocationSummaryDto[]>(response);
        Assert.Empty(invocations);
    }

    [Fact]
    public async Task AgentInvocations_FeatureNotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/features/{Guid.NewGuid()}/agent-invocations");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AgentInvocations_AfterPrdGeneration_ReturnsInvocationRecord()
    {
        var project = await CreateProject("InvocationsPrd", "org/invocations-prd");
        var feature = await CreateFeature(project.Id, "PRD invocation test");

        await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);

        var response = await _client.GetAsync($"/api/features/{feature.Id}/agent-invocations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var invocations = await Deserialize<AgentInvocationSummaryDto[]>(response);
        Assert.Single(invocations);
        Assert.Equal(feature.Id, invocations[0].FeatureId);
        Assert.Contains("Prd", invocations[0].AgentName, StringComparison.OrdinalIgnoreCase);
        Assert.True(invocations[0].DurationMs >= 0);
    }

    [Fact]
    public async Task AgentInvocationDetail_ReturnsFullPromptAndResponse()
    {
        var project = await CreateProject("InvocationDetail", "org/invocation-detail");
        var feature = await CreateFeature(project.Id, "Invocation detail test");

        await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);

        // Get the summary to find the invocation ID
        var listResponse = await _client.GetAsync($"/api/features/{feature.Id}/agent-invocations");
        var invocations = await Deserialize<AgentInvocationSummaryDto[]>(listResponse);
        var invocationId = invocations[0].Id;

        // Get the detail
        var response = await _client.GetAsync(
            $"/api/features/{feature.Id}/agent-invocations/{invocationId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await Deserialize<AgentInvocationDetailDto>(response);
        Assert.Equal(invocationId, detail.Id);
        Assert.Equal(feature.Id, detail.FeatureId);
        Assert.NotEmpty(detail.SystemPrompt);
        Assert.NotEmpty(detail.UserMessage);
        Assert.NotEmpty(detail.Response);
    }

    [Fact]
    public async Task AgentInvocationDetail_NotFound_Returns404()
    {
        var project = await CreateProject("InvocationDetailNotFound", "org/invocation-detail-nf");
        var feature = await CreateFeature(project.Id, "Invocation detail not found");

        var response = await _client.GetAsync(
            $"/api/features/{feature.Id}/agent-invocations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AgentInvocationDetail_WrongFeature_Returns404()
    {
        var project = await CreateProject("InvocationWrongFeature", "org/invocation-wrong-feat");
        var feature1 = await CreateFeature(project.Id, "Feature 1");
        var feature2 = await CreateFeature(project.Id, "Feature 2");

        // Generate PRD for feature1 to create an invocation
        await _client.PostAsync($"/api/features/{feature1.Id}/generate-prd", null);
        var listResponse = await _client.GetAsync($"/api/features/{feature1.Id}/agent-invocations");
        var invocations = await Deserialize<AgentInvocationSummaryDto[]>(listResponse);
        var invocationId = invocations[0].Id;

        // Try to access invocation from feature2 — should return 404
        var response = await _client.GetAsync(
            $"/api/features/{feature2.Id}/agent-invocations/{invocationId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Rerun Step
    // ──────────────────────────────────────────────

    [Fact]
    public async Task RerunStep_WithPrdStep_RerunsAndReturnsFeature()
    {
        var project = await CreateProject("RerunPrd", "org/rerun-prd");
        var feature = await CreateFeature(project.Id, "Rerun PRD step test");

        // Advance to PrdPending
        await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);

        // Rerun with explicit step="prd"
        var response = await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/rerun-step",
            new { step = "prd" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedFeature = await Deserialize<FeatureDto>(response);
        Assert.Equal(feature.Id, updatedFeature.Id);
        Assert.Equal("PrdPending", updatedFeature.Status);
    }

    [Fact]
    public async Task RerunStep_WithNoStep_RerunsCurrentStatus()
    {
        var project = await CreateProject("RerunNoStep", "org/rerun-no-step");
        var feature = await CreateFeature(project.Id, "Rerun current step");

        // Advance to PrdPending
        await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);

        // Rerun with no step — should re-run based on current status (PrdPending → prd)
        var response = await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/rerun-step",
            new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedFeature = await Deserialize<FeatureDto>(response);
        Assert.Equal("PrdPending", updatedFeature.Status);
    }

    [Fact]
    public async Task RerunStep_WithUnknownStep_ReturnsBadRequest()
    {
        var project = await CreateProject("RerunUnknownStep", "org/rerun-unknown");
        var feature = await CreateFeature(project.Id, "Rerun unknown step");

        var response = await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/rerun-step",
            new { step = "nonexistent" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Artifact Update Content
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ArtifactUpdateContent_WhenValid_UpdatesGitHubFile()
    {
        var project = await CreateProject("UpdateContentProject", "org/update-content");
        var feature = await CreateFeature(project.Id, "Update content test");

        var prd = await GeneratePrdAndGetArtifact(feature.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}/content",
            new { content = "# Updated PRD\n\nThis is the updated content." });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var artifact = await Deserialize<ArtifactDto>(response);
        Assert.Equal(prd.Id, artifact.Id);
    }

    [Fact]
    public async Task ArtifactUpdateContent_WhenArtifactNotFound_Returns404()
    {
        var project = await CreateProject("UpdateContentNotFound", "org/update-content-nf");
        var feature = await CreateFeature(project.Id, "Update content not found");

        var response = await _client.PutAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{Guid.NewGuid()}/content",
            new { content = "# Content" });
        // ArtifactNotFoundException maps to 404 via middleware
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Artifact Retry Commit
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ArtifactRetryCommit_WhenArtifactAlreadyCommitted_ReturnsBadRequest()
    {
        var project = await CreateProject("RetryCommitProject", "org/retry-commit");
        var feature = await CreateFeature(project.Id, "Retry commit test");

        // Generate PRD — produces a CommitSucceeded=true artifact
        var prd = await GeneratePrdAndGetArtifact(feature.Id);

        // Retry commit on an already-committed artifact throws InvalidOperationException → 400
        var response = await _client.PostAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}/retry-commit", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ArtifactRetryCommit_WhenArtifactNotFound_Returns404()
    {
        var project = await CreateProject("RetryCommitNotFound", "org/retry-commit-nf");
        var feature = await CreateFeature(project.Id, "Retry commit not found");

        var response = await _client.PostAsync(
            $"/api/features/{feature.Id}/artifacts/{Guid.NewGuid()}/retry-commit", null);
        // ArtifactNotFoundException → 404
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private async Task<ProjectDto> CreateProject(string name, string repoSlug)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            name,
            description = $"Project {name}",
            config = $"project:\n  name: {name.ToLower()}",
            repositorySlug = repoSlug,
            gitHubToken = "fake-token"
        });
        response.EnsureSuccessStatusCode();
        return await Deserialize<ProjectDto>(response);
    }

    private async Task<FeatureDto> CreateFeature(Guid projectId, string prompt)
    {
        var response = await _client.PostAsJsonAsync("/api/features", new { projectId, prompt });
        response.EnsureSuccessStatusCode();
        return await Deserialize<FeatureDto>(response);
    }

    private async Task<ArtifactDto> GeneratePrdAndGetArtifact(Guid featureId)
    {
        var response = await _client.PostAsync($"/api/features/{featureId}/generate-prd", null);
        response.EnsureSuccessStatusCode();
        return await Deserialize<ArtifactDto>(response);
    }

    private static async Task<T> Deserialize<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
    }

    // DTOs
    private record ProjectDto(Guid Id, string Name, string RepositorySlug);
    private record FeatureDto(Guid Id, Guid ProjectId, string Prompt, string Status);
    private record ArtifactDto(Guid Id, Guid FeatureId, string Type, string RepositoryPath, string? ApprovedBy, DateTime? ApprovedAt, bool CommitSucceeded);
    private record AgentInvocationSummaryDto(Guid Id, Guid FeatureId, string AgentName, DateTime InvokedAt, int DurationMs);
    private record AgentInvocationDetailDto(Guid Id, Guid FeatureId, string AgentName, string SystemPrompt, string UserMessage, string Response, DateTime InvokedAt, int DurationMs);
}
