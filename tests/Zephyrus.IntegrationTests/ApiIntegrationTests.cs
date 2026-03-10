using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Zephyrus.IntegrationTests;

public class ApiIntegrationTests : IClassFixture<ZephyrusApiFactory>
{
    private readonly HttpClient _client;
    private readonly ZephyrusApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiIntegrationTests(ZephyrusApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ──────────────────────────────────────────────
    // Projects API
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Projects_CreateAndRetrieve()
    {
        // POST /api/projects
        var createResponse = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "Alpha",
            description = "First project",
            config = "project:\n  name: alpha",
            repositorySlug = "org/alpha"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await Deserialize<ProjectDto>(createResponse);
        Assert.Equal("Alpha", created.Name);
        Assert.Equal("org/alpha", created.RepositorySlug);

        // GET /api/projects/{id}
        var getResponse = await _client.GetAsync($"/api/projects/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await Deserialize<ProjectDto>(getResponse);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Alpha", fetched.Name);
    }

    [Fact]
    public async Task Projects_GetAll_ReturnsArray()
    {
        // Seed a project
        await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "ListTest",
            description = "For listing",
            config = "config: test",
            repositorySlug = "org/list-test"
        });

        // GET /api/projects
        var response = await _client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var projects = await Deserialize<ProjectDto[]>(response);
        Assert.Contains(projects, p => p.Name == "ListTest");
    }

    [Fact]
    public async Task Projects_GetById_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Features API
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Features_CreateAndRetrieve()
    {
        var project = await CreateProject("FeatureTest", "org/feature-test");

        // POST /api/features
        var createResponse = await _client.PostAsJsonAsync("/api/features", new
        {
            projectId = project.Id,
            prompt = "Add login page"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await Deserialize<FeatureDto>(createResponse);
        Assert.Equal("Add login page", created.Prompt);
        Assert.Equal("Ideation", created.Status);

        // GET /api/features/{id}
        var getResponse = await _client.GetAsync($"/api/features/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await Deserialize<FeatureDto>(getResponse);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task Features_GetById_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/features/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Features_GetByProject_ReturnsFilteredList()
    {
        var project = await CreateProject("ByProjectTest", "org/by-project-test");

        await _client.PostAsJsonAsync("/api/features", new { projectId = project.Id, prompt = "Feature A" });
        await _client.PostAsJsonAsync("/api/features", new { projectId = project.Id, prompt = "Feature B" });

        // GET /api/features/by-project/{projectId}
        var response = await _client.GetAsync($"/api/features/by-project/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var features = await Deserialize<FeatureDto[]>(response);
        Assert.Equal(2, features.Length);
        Assert.Contains(features, f => f.Prompt == "Feature A");
        Assert.Contains(features, f => f.Prompt == "Feature B");
    }

    // ──────────────────────────────────────────────
    // PRD Generation
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GeneratePrd_ReturnsArtifactAndAdvancesStatus()
    {
        var project = await CreateProject("PrdGenTest", "org/prd-gen-test");
        var feature = await CreateFeature(project.Id, "Build dashboard");

        // POST /api/features/{id}/generate-prd
        var response = await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var artifact = await Deserialize<ArtifactDto>(response);
        Assert.Equal("Prd", artifact.Type);
        Assert.Contains("prd-", artifact.RepositoryPath);
        Assert.Null(artifact.ApprovedBy);

        // Feature should now be PrdPending
        var featureResponse = await _client.GetAsync($"/api/features/{feature.Id}");
        var updated = await Deserialize<FeatureDto>(featureResponse);
        Assert.Equal("PrdPending", updated.Status);
    }

    [Fact]
    public async Task GeneratePrd_WrongStatus_Throws()
    {
        var project = await CreateProject("PrdWrongStatus", "org/prd-wrong-status");
        var feature = await CreateFeature(project.Id, "Some feature");

        // Generate once — moves to PrdPending
        await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);

        // Generate again — should fail (not in Ideation)
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null));
    }

    // ──────────────────────────────────────────────
    // Artifacts List
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Artifacts_List_EmptyInitially()
    {
        var project = await CreateProject("ArtifactListEmpty", "org/artifact-list-empty");
        var feature = await CreateFeature(project.Id, "Artifact list test");

        // GET /api/features/{id}/artifacts
        var response = await _client.GetAsync($"/api/features/{feature.Id}/artifacts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var artifacts = await Deserialize<ArtifactDto[]>(response);
        Assert.Empty(artifacts);
    }

    [Fact]
    public async Task Artifacts_List_AfterPrdGeneration()
    {
        var project = await CreateProject("ArtifactListPrd", "org/artifact-list-prd");
        var feature = await CreateFeature(project.Id, "Artifact after PRD");

        await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);

        var response = await _client.GetAsync($"/api/features/{feature.Id}/artifacts");
        var artifacts = await Deserialize<ArtifactDto[]>(response);

        Assert.Single(artifacts);
        Assert.Equal("Prd", artifacts[0].Type);
    }

    [Fact]
    public async Task Artifacts_List_FeatureNotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/features/{Guid.NewGuid()}/artifacts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Artifact Content
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ArtifactContent_ReturnsPrdMarkdown()
    {
        var project = await CreateProject("ContentTest", "org/content-test");
        var feature = await CreateFeature(project.Id, "Content retrieval");

        var prdResponse = await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);
        var prd = await Deserialize<ArtifactDto>(prdResponse);

        // GET /api/features/{id}/artifacts/{artifactId}/content
        var response = await _client.GetAsync($"/api/features/{feature.Id}/artifacts/{prd.Id}/content");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await Deserialize<ContentDto>(response);
        Assert.Contains("PRD", body.Content);
    }

    [Fact]
    public async Task ArtifactContent_WrongFeatureId_Returns404()
    {
        var project = await CreateProject("ContentWrong", "org/content-wrong");
        var feature = await CreateFeature(project.Id, "Wrong feature content");

        var prdResponse = await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);
        var prd = await Deserialize<ArtifactDto>(prdResponse);

        // Use a different feature ID
        var response = await _client.GetAsync($"/api/features/{Guid.NewGuid()}/artifacts/{prd.Id}/content");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArtifactContent_NonexistentArtifact_Returns404()
    {
        var project = await CreateProject("ContentNoArtifact", "org/content-no-artifact");
        var feature = await CreateFeature(project.Id, "No artifact");

        var response = await _client.GetAsync($"/api/features/{feature.Id}/artifacts/{Guid.NewGuid()}/content");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Approval Gate
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Approve_PrdArtifact_AdvancesToPrdApproved()
    {
        var project = await CreateProject("ApproveTest", "org/approve-test");
        var feature = await CreateFeature(project.Id, "Approval test");

        var prdResponse = await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);
        var prd = await Deserialize<ArtifactDto>(prdResponse);

        // POST /api/features/{id}/artifacts/{artifactId}/approve
        var approveResponse = await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}/approve",
            new { approvedBy = "pm@test.com" });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var approved = await Deserialize<ArtifactDto>(approveResponse);
        Assert.Equal("pm@test.com", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovedAt);
    }

    [Fact]
    public async Task Approve_TriggersArchitectAgent_FeatureEndsAtArchPending()
    {
        var project = await CreateProject("OrchestratorTest", "org/orchestrator-test");
        var feature = await CreateFeature(project.Id, "Orchestrator chain");

        // Generate PRD
        var prdResponse = await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);
        var prd = await Deserialize<ArtifactDto>(prdResponse);

        // Approve PRD — triggers orchestrator → Architect Agent
        await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}/approve",
            new { approvedBy = "pm@test.com" });

        // Feature should be at ArchPending (orchestrator advanced it)
        var featureResponse = await _client.GetAsync($"/api/features/{feature.Id}");
        var updated = await Deserialize<FeatureDto>(featureResponse);
        Assert.Equal("ArchPending", updated.Status);

        // ADR artifact should exist
        var artifactsResponse = await _client.GetAsync($"/api/features/{feature.Id}/artifacts");
        var artifacts = await Deserialize<ArtifactDto[]>(artifactsResponse);
        Assert.Equal(2, artifacts.Length);
        Assert.Contains(artifacts, a => a.Type == "Prd");
        Assert.Contains(artifacts, a => a.Type == "Adr");
    }

    [Fact]
    public async Task Approve_AdrArtifact_AdvancesToArchApproved()
    {
        var project = await CreateProject("AdrApproveTest", "org/adr-approve-test");
        var feature = await CreateFeature(project.Id, "ADR approval flow");

        // Generate PRD → Approve PRD → Architect Agent runs → ADR created
        var prdResponse = await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);
        var prd = await Deserialize<ArtifactDto>(prdResponse);

        await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}/approve",
            new { approvedBy = "pm@test.com" });

        // Find the ADR artifact
        var artifactsResponse = await _client.GetAsync($"/api/features/{feature.Id}/artifacts");
        var artifacts = await Deserialize<ArtifactDto[]>(artifactsResponse);
        var adr = artifacts.Single(a => a.Type == "Adr");

        // Approve ADR
        var approveResponse = await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{adr.Id}/approve",
            new { approvedBy = "techlead@test.com" });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // Feature should be at ArchApproved
        var featureResponse = await _client.GetAsync($"/api/features/{feature.Id}");
        var updated = await Deserialize<FeatureDto>(featureResponse);
        Assert.Equal("ArchApproved", updated.Status);
    }

    [Fact]
    public async Task Approve_AlreadyApproved_Throws()
    {
        var project = await CreateProject("DoubleApprove", "org/double-approve");
        var feature = await CreateFeature(project.Id, "Double approve api");

        var prdResponse = await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);
        var prd = await Deserialize<ArtifactDto>(prdResponse);

        // First approve — OK
        await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}/approve",
            new { approvedBy = "pm@test.com" });

        // Second approve — should fail
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.PostAsJsonAsync(
                $"/api/features/{feature.Id}/artifacts/{prd.Id}/approve",
                new { approvedBy = "other@test.com" }));
    }

    [Fact]
    public async Task Approve_WrongFeatureStatus_Throws()
    {
        var project = await CreateProject("WrongStatusApi", "org/wrong-status-api");
        var feature = await CreateFeature(project.Id, "Wrong status via API");

        // Generate PRD → Approve PRD → now at ArchPending
        var prdResponse = await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);
        var prd = await Deserialize<ArtifactDto>(prdResponse);

        await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}/approve",
            new { approvedBy = "pm@test.com" });

        // Try to approve the PRD again — feature is no longer in PrdPending
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.PostAsJsonAsync(
                $"/api/features/{feature.Id}/artifacts/{prd.Id}/approve",
                new { approvedBy = "pm@test.com" }));
    }

    // ──────────────────────────────────────────────
    // ADR Content
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AdrContent_ReturnsArchitectureMarkdown()
    {
        var project = await CreateProject("AdrContentTest", "org/adr-content-test");
        var feature = await CreateFeature(project.Id, "ADR content retrieval");

        // Generate PRD → Approve → Architect Agent fires → ADR exists
        var prdResponse = await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);
        var prd = await Deserialize<ArtifactDto>(prdResponse);

        await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}/approve",
            new { approvedBy = "pm@test.com" });

        var artifactsResponse = await _client.GetAsync($"/api/features/{feature.Id}/artifacts");
        var artifacts = await Deserialize<ArtifactDto[]>(artifactsResponse);
        var adr = artifacts.Single(a => a.Type == "Adr");

        // GET content
        var response = await _client.GetAsync($"/api/features/{feature.Id}/artifacts/{adr.Id}/content");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await Deserialize<ContentDto>(response);
        Assert.Contains("ADR", body.Content);
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
            repositorySlug = repoSlug
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

    private static async Task<T> Deserialize<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
    }

    // DTOs for deserialization
    private record ProjectDto(Guid Id, string Name, string Description, string RepositorySlug, DateTime CreatedAt);
    private record FeatureDto(Guid Id, Guid ProjectId, string Prompt, string Status, DateTime CreatedAt);
    private record ArtifactDto(Guid Id, Guid FeatureId, string Type, string RepositoryPath, string? ApprovedBy, DateTime? ApprovedAt);
    private record ContentDto(string Content);
}
