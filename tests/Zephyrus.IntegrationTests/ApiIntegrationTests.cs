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
            repositorySlug = "org/alpha",
            gitHubToken = "fake-token"
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
            repositorySlug = "org/list-test",
            gitHubToken = "fake-token"
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
        Assert.StartsWith("docs/prd/", artifact.RepositoryPath);
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

        // Generate PRD — moves to PrdPending
        var prdResponse = await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);
        var prd = await prdResponse.Content.ReadFromJsonAsync<ArtifactDto>();

        // Approve PRD — moves to PrdApproved, then orchestrator advances to ArchPending
        await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{prd!.Id}/approve",
            new { approvedBy = "pm@test.com" });

        // Generate PRD again — should fail (not in Ideation or PrdPending)
        var response = await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

        // Feature should be at TasksPending (orchestrator triggers Task Agent after ADR approval)
        var featureResponse = await _client.GetAsync($"/api/features/{feature.Id}");
        var updated = await Deserialize<FeatureDto>(featureResponse);
        Assert.Equal("TasksPending", updated.Status);
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
        var secondApprove = await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}/approve",
            new { approvedBy = "other@test.com" });
        Assert.Equal(HttpStatusCode.BadRequest, secondApprove.StatusCode);
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
        var wrongStatusApprove = await _client.PostAsJsonAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}/approve",
            new { approvedBy = "pm@test.com" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongStatusApprove.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Pipeline Events
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PipelineEvents_ReturnsTransitionHistory()
    {
        var project = await CreateProject("EventsTest", "org/events-test");
        var feature = await CreateFeature(project.Id, "Pipeline events test");

        // Generate PRD → Ideation → PrdPending
        await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);

        // GET /api/features/{id}/pipeline-events
        var response = await _client.GetAsync($"/api/features/{feature.Id}/pipeline-events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await Deserialize<PipelineEventDto[]>(response);
        Assert.Single(events);
        Assert.Equal("Ideation", events[0].FromStatus);
        Assert.Equal("PrdPending", events[0].ToStatus);
    }

    [Fact]
    public async Task PipelineEvents_FeatureNotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/features/{Guid.NewGuid()}/pipeline-events");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Tasks API
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Tasks_EmptyInitially()
    {
        var project = await CreateProject("TasksEmpty", "org/tasks-empty");
        var feature = await CreateFeature(project.Id, "Tasks empty test");

        var response = await _client.GetAsync($"/api/features/{feature.Id}/tasks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tasks = await Deserialize<TaskItemDto[]>(response);
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task Tasks_FeatureNotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/features/{Guid.NewGuid()}/tasks");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Tasks_AfterTaskAgentRuns_ReturnsTaskItems()
    {
        var project = await CreateProject("TasksAfterAgent", "org/tasks-after-agent");
        var feature = await CreateFeature(project.Id, "Tasks after agent");

        // PRD → Approve → Architect → Approve ADR → Task Agent runs
        var prd = await GeneratePrdAndGetArtifact(feature.Id);
        await ApproveArtifact(feature.Id, prd.Id, "pm@test.com");

        var adr = await GetArtifactByType(feature.Id, "Adr");
        await ApproveArtifact(feature.Id, adr.Id, "tl@test.com");

        // Feature should now be at TasksPending with tasks created
        var response = await _client.GetAsync($"/api/features/{feature.Id}/tasks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tasks = await Deserialize<TaskItemDto[]>(response);
        Assert.Equal(3, tasks.Length);
        Assert.Contains(tasks, t => t.AgentType == "DB");
        Assert.Contains(tasks, t => t.AgentType == "BE");
        Assert.Contains(tasks, t => t.AgentType == "FE");
        Assert.All(tasks, t =>
        {
            Assert.Equal("Pending", t.Status);
            Assert.NotNull(t.ExternalIssueId);
            Assert.NotEmpty(t.Title);
        });
    }

    [Fact]
    public async Task Tasks_AfterCodeAgentRuns_HavePrIds()
    {
        var project = await CreateProject("TasksWithPrs", "org/tasks-with-prs");
        var feature = await CreateFeature(project.Id, "Tasks with PRs");

        // Run through to Coding (Task Agent → approve → Code Agent)
        var prd = await GeneratePrdAndGetArtifact(feature.Id);
        await ApproveArtifact(feature.Id, prd.Id, "pm@test.com");
        var adr = await GetArtifactByType(feature.Id, "Adr");
        await ApproveArtifact(feature.Id, adr.Id, "tl@test.com");
        var task = await GetArtifactByType(feature.Id, "Task");
        await ApproveArtifact(feature.Id, task.Id, "tl@test.com");

        // Feature is now in Coding — tasks should have PrIds
        var featureResponse = await _client.GetAsync($"/api/features/{feature.Id}");
        var updated = await Deserialize<FeatureDto>(featureResponse);
        Assert.Equal("Coding", updated.Status);

        var response = await _client.GetAsync($"/api/features/{feature.Id}/tasks");
        var tasks = await Deserialize<TaskItemDto[]>(response);
        Assert.Equal(3, tasks.Length);
        Assert.All(tasks, t =>
        {
            Assert.Equal("PrOpen", t.Status);
            Assert.NotNull(t.PrId);
        });
    }

    // ──────────────────────────────────────────────
    // Pipeline Events — Multi-Transition
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PipelineEvents_AfterApprovalChain_ReturnsMultipleTransitions()
    {
        var project = await CreateProject("EventsChain", "org/events-chain");
        var feature = await CreateFeature(project.Id, "Events chain test");

        // PRD → Approve → Architect runs (3 transitions: Ideation→PrdPending, PrdPending→PrdApproved, PrdApproved→ArchPending)
        var prd = await GeneratePrdAndGetArtifact(feature.Id);
        await ApproveArtifact(feature.Id, prd.Id, "pm@test.com");

        var response = await _client.GetAsync($"/api/features/{feature.Id}/pipeline-events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await Deserialize<PipelineEventDto[]>(response);
        Assert.Equal(3, events.Length);

        // Verify chronological order and transition chain
        Assert.Equal("Ideation", events[0].FromStatus);
        Assert.Equal("PrdPending", events[0].ToStatus);
        Assert.Equal("system", events[0].TriggeredBy);

        Assert.Equal("PrdPending", events[1].FromStatus);
        Assert.Equal("PrdApproved", events[1].ToStatus);
        Assert.Equal("pm@test.com", events[1].TriggeredBy);

        Assert.Equal("PrdApproved", events[2].FromStatus);
        Assert.Equal("ArchPending", events[2].ToStatus);
        Assert.Equal("system", events[2].TriggeredBy);
    }

    [Fact]
    public async Task PipelineEvents_EmptyForNewFeature()
    {
        var project = await CreateProject("EventsEmpty", "org/events-empty");
        var feature = await CreateFeature(project.Id, "Events empty test");

        var response = await _client.GetAsync($"/api/features/{feature.Id}/pipeline-events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await Deserialize<PipelineEventDto[]>(response);
        Assert.Empty(events);
    }

    // ──────────────────────────────────────────────
    // Full Pipeline via HTTP API
    // ──────────────────────────────────────────────

    [Fact]
    public async Task FullPipelineViaApi_IdeationThroughDeployed()
    {
        var project = await CreateProject("FullApiPipeline", "org/full-api-pipeline");
        var feature = await CreateFeature(project.Id, "Full pipeline via API");

        // Step 1: Generate PRD → PrdPending
        var prd = await GeneratePrdAndGetArtifact(feature.Id);
        await AssertFeatureStatus(feature.Id, "PrdPending");

        // Step 2: Approve PRD → PrdApproved → Architect runs → ArchPending
        await ApproveArtifact(feature.Id, prd.Id, "pm@test.com");
        await AssertFeatureStatus(feature.Id, "ArchPending");

        // Step 3: Approve ADR → ArchApproved → Task Agent runs → TasksPending
        var adr = await GetArtifactByType(feature.Id, "Adr");
        await ApproveArtifact(feature.Id, adr.Id, "tl@test.com");
        await AssertFeatureStatus(feature.Id, "TasksPending");

        // Step 4: Approve Tasks → TasksApproved → Code Agents run → Coding
        var task = await GetArtifactByType(feature.Id, "Task");
        await ApproveArtifact(feature.Id, task.Id, "tl@test.com");
        await AssertFeatureStatus(feature.Id, "Coding");

        // Step 5: Approve PR → Coding → QaPending, QA Agent runs
        var pr = await GetArtifactByType(feature.Id, "Pr");
        await ApproveArtifact(feature.Id, pr.Id, "tl@test.com");
        await AssertFeatureStatus(feature.Id, "QaPending");

        // Step 6: Approve Test → QaApproved, DevOps Agent runs
        var test = await GetArtifactByType(feature.Id, "Test");
        await ApproveArtifact(feature.Id, test.Id, "qa@test.com");
        await AssertFeatureStatus(feature.Id, "QaApproved");

        // Step 7: Approve Workflow → Deployed
        var workflow = await GetArtifactByType(feature.Id, "Workflow");
        await ApproveArtifact(feature.Id, workflow.Id, "tl@test.com");
        await AssertFeatureStatus(feature.Id, "Deployed");

        // Verify all artifacts exist
        var artifactsResponse = await _client.GetAsync($"/api/features/{feature.Id}/artifacts");
        var artifacts = await Deserialize<ArtifactDto[]>(artifactsResponse);
        Assert.Equal(6, artifacts.Length);
        Assert.Contains(artifacts, a => a.Type == "Prd");
        Assert.Contains(artifacts, a => a.Type == "Adr");
        Assert.Contains(artifacts, a => a.Type == "Task");
        Assert.Contains(artifacts, a => a.Type == "Pr");
        Assert.Contains(artifacts, a => a.Type == "Test");
        Assert.Contains(artifacts, a => a.Type == "Workflow");
        Assert.All(artifacts, a => Assert.NotNull(a.ApprovedBy));

        // Verify 10 pipeline events (full pipeline)
        var eventsResponse = await _client.GetAsync($"/api/features/{feature.Id}/pipeline-events");
        var events = await Deserialize<PipelineEventDto[]>(eventsResponse);
        Assert.Equal(10, events.Length);
        Assert.Equal("Ideation", events[0].FromStatus);
        Assert.Equal("PrdPending", events[0].ToStatus);
        Assert.Equal("QaApproved", events[9].FromStatus);
        Assert.Equal("Deployed", events[9].ToStatus);

        // Verify tasks have PRs
        var tasksResponse = await _client.GetAsync($"/api/features/{feature.Id}/tasks");
        var tasks = await Deserialize<TaskItemDto[]>(tasksResponse);
        Assert.Equal(3, tasks.Length);
        Assert.All(tasks, t => Assert.NotNull(t.PrId));
    }

    [Fact]
    public async Task WorkflowArtifactContent_ReturnsYaml()
    {
        var project = await CreateProject("WorkflowContent", "org/workflow-content");
        var feature = await CreateFeature(project.Id, "Workflow content test");

        // Run through full pipeline to QaApproved (DevOps Agent creates workflow)
        var prd = await GeneratePrdAndGetArtifact(feature.Id);
        await ApproveArtifact(feature.Id, prd.Id, "pm@test.com");
        var adr = await GetArtifactByType(feature.Id, "Adr");
        await ApproveArtifact(feature.Id, adr.Id, "tl@test.com");
        var task = await GetArtifactByType(feature.Id, "Task");
        await ApproveArtifact(feature.Id, task.Id, "tl@test.com");
        var pr = await GetArtifactByType(feature.Id, "Pr");
        await ApproveArtifact(feature.Id, pr.Id, "tl@test.com");
        var test = await GetArtifactByType(feature.Id, "Test");
        await ApproveArtifact(feature.Id, test.Id, "qa@test.com");

        // Get workflow artifact content
        var workflow = await GetArtifactByType(feature.Id, "Workflow");
        var response = await _client.GetAsync(
            $"/api/features/{feature.Id}/artifacts/{workflow.Id}/content");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await Deserialize<ContentDto>(response);
        Assert.Contains("Deploy", body.Content);
        Assert.Contains("dotnet", body.Content);
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
    // Delete — Projects
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Projects_DeletionPreview_ReturnsEntityInfo()
    {
        var project = await CreateProject("DeletePreviewProject", "org/delete-preview");
        await CreateFeature(project.Id, "Feature to be deleted");

        var response = await _client.GetAsync($"/api/projects/{project.Id}/deletion-preview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await Deserialize<DeletionPreviewDto>(response);
        Assert.Equal("DeletePreviewProject", preview.EntityTitle);
        Assert.Equal(1, preview.ChildrenCount);
        Assert.NotEmpty(preview.Warnings);
    }

    [Fact]
    public async Task Projects_DeletionPreview_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/deletion-preview");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Projects_Delete_RemovesProjectAndFeatures()
    {
        var project = await CreateProject("ToDeleteProject", "org/to-delete");
        var feature = await CreateFeature(project.Id, "Feature inside deleted project");

        var deleteResponse = await _client.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var deleted = await Deserialize<DeletedDto>(deleteResponse);
        Assert.True(deleted.DeletedEntitiesCount >= 1);

        // Project should be gone
        var getResponse = await _client.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        // Feature should be gone
        var featureResponse = await _client.GetAsync($"/api/features/{feature.Id}");
        Assert.Equal(HttpStatusCode.NotFound, featureResponse.StatusCode);
    }

    [Fact]
    public async Task Projects_Delete_NotFound_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Delete — Features
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Features_DeletionPreview_ReturnsEntityInfo()
    {
        var project = await CreateProject("FeatureDeletePreviewProj", "org/feat-del-preview");
        var feature = await CreateFeature(project.Id, "Feature to preview delete");
        await _client.PostAsync($"/api/features/{feature.Id}/generate-prd", null);

        var response = await _client.GetAsync($"/api/features/{feature.Id}/deletion-preview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await Deserialize<DeletionPreviewDto>(response);
        Assert.Equal("Feature to preview delete", preview.EntityTitle);
        Assert.Equal(1, preview.ChildrenCount);
    }

    [Fact]
    public async Task Features_DeletionPreview_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/features/{Guid.NewGuid()}/deletion-preview");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Features_Delete_RemovesFeatureAndArtifacts()
    {
        var project = await CreateProject("FeatureDeleteProj", "org/feat-delete");
        var feature = await CreateFeature(project.Id, "Feature to be deleted");
        var prd = await GeneratePrdAndGetArtifact(feature.Id);

        var deleteResponse = await _client.DeleteAsync($"/api/features/{feature.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var deleted = await Deserialize<DeletedDto>(deleteResponse);
        Assert.True(deleted.DeletedEntitiesCount >= 1);

        // Feature should be gone
        var featureResponse = await _client.GetAsync($"/api/features/{feature.Id}");
        Assert.Equal(HttpStatusCode.NotFound, featureResponse.StatusCode);
    }

    [Fact]
    public async Task Features_Delete_NotFound_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/features/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────
    // Delete — Artifacts
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Artifacts_DeletionPreview_ReturnsEntityInfo()
    {
        var project = await CreateProject("ArtifactDeletePreviewProj", "org/art-del-preview");
        var feature = await CreateFeature(project.Id, "Artifact delete preview");
        var prd = await GeneratePrdAndGetArtifact(feature.Id);

        var response = await _client.GetAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}/deletion-preview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await Deserialize<DeletionPreviewDto>(response);
        Assert.Equal("Prd", preview.EntityTitle);
        Assert.Equal(0, preview.ChildrenCount);
    }

    [Fact]
    public async Task Artifacts_DeletionPreview_NotFound_Returns404()
    {
        var response = await _client.GetAsync(
            $"/api/features/{Guid.NewGuid()}/artifacts/{Guid.NewGuid()}/deletion-preview");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Artifacts_Delete_RemovesArtifact()
    {
        var project = await CreateProject("ArtifactDeleteProj", "org/art-delete");
        var feature = await CreateFeature(project.Id, "Artifact to delete");
        var prd = await GeneratePrdAndGetArtifact(feature.Id);

        var deleteResponse = await _client.DeleteAsync(
            $"/api/features/{feature.Id}/artifacts/{prd.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var deleted = await Deserialize<DeletedDto>(deleteResponse);
        Assert.Equal(1, deleted.DeletedEntitiesCount);

        // Artifact should be gone from the list
        var artifactsResponse = await _client.GetAsync($"/api/features/{feature.Id}/artifacts");
        var artifacts = await Deserialize<ArtifactDto[]>(artifactsResponse);
        Assert.DoesNotContain(artifacts, a => a.Id == prd.Id);
    }

    [Fact]
    public async Task Artifacts_Delete_NotFound_Returns404()
    {
        var project = await CreateProject("ArtifactDeleteNotFound", "org/art-del-notfound");
        var feature = await CreateFeature(project.Id, "Artifact not found delete");

        var response = await _client.DeleteAsync(
            $"/api/features/{feature.Id}/artifacts/{Guid.NewGuid()}");
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

    private async Task ApproveArtifact(Guid featureId, Guid artifactId, string approvedBy)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/features/{featureId}/artifacts/{artifactId}/approve",
            new { approvedBy });
        response.EnsureSuccessStatusCode();
    }

    private async Task<ArtifactDto> GetArtifactByType(Guid featureId, string type)
    {
        var response = await _client.GetAsync($"/api/features/{featureId}/artifacts");
        response.EnsureSuccessStatusCode();
        var artifacts = await Deserialize<ArtifactDto[]>(response);
        return artifacts.Single(a => a.Type == type);
    }

    private async Task AssertFeatureStatus(Guid featureId, string expectedStatus)
    {
        var response = await _client.GetAsync($"/api/features/{featureId}");
        response.EnsureSuccessStatusCode();
        var feature = await Deserialize<FeatureDto>(response);
        Assert.Equal(expectedStatus, feature.Status);
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
    private record TaskItemDto(Guid Id, Guid FeatureId, string Title, string Status, string AgentType, int? ExternalIssueId, int? PrId);
    private record ContentDto(string Content);
    private record PipelineEventDto(Guid Id, Guid FeatureId, string FromStatus, string ToStatus, string TriggeredBy, DateTime Timestamp);
    private record DeletionPreviewDto(string EntityTitle, int ChildrenCount, string[] Warnings);
    private record DeletedDto(int DeletedEntitiesCount);
}
