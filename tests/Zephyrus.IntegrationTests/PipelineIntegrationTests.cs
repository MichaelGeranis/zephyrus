using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zephyrus.Application.Managers;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.IntegrationTests;

public class PipelineIntegrationTests : IClassFixture<PipelineFixture>
{
    private readonly PipelineFixture _fixture;

    public PipelineIntegrationTests(PipelineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FullPipeline_IdeationThroughArchPending_WorksEndToEnd()
    {
        // --- Arrange: Create a project and feature ---
        Guid projectId;
        Guid featureId;

        using (var scope = _fixture.CreateScope())
        {
            var projectManager = scope.ServiceProvider.GetRequiredService<ProjectManager>();
            var project = await projectManager.CreateAsync(
                "Test Project",
                "A project for integration testing",
                "project:\n  name: test-project\nstack:\n  backend: .NET",
                "test-owner/test-repo");
            projectId = project.Id;

            var featureManager = scope.ServiceProvider.GetRequiredService<FeatureManager>();
            var feature = await featureManager.CreateAsync(projectId, "Add user authentication");
            featureId = feature.Id;

            Assert.Equal(FeatureStatus.Ideation, feature.Status);
        }

        // --- Act 1: Generate PRD ---
        Guid prdArtifactId;

        using (var scope = _fixture.CreateScope())
        {
            var generatePrd = scope.ServiceProvider.GetRequiredService<InvokePrdAgentUseCase>();
            var prdArtifact = await generatePrd.ExecuteAsync(featureId);

            prdArtifactId = prdArtifact.Id;

            Assert.Equal(ArtifactType.Prd, prdArtifact.Type);
            Assert.Contains("prd-", prdArtifact.RepositoryPath);
        }

        // --- Assert 1: Feature is now PrdPending, PRD committed to fake repo ---
        using (var scope = _fixture.CreateScope())
        {
            var featureRepo = scope.ServiceProvider.GetRequiredService<IFeatureRepository>();
            var feature = await featureRepo.GetByIdAsync(featureId);

            Assert.NotNull(feature);
            Assert.Equal(FeatureStatus.PrdPending, feature!.Status);
        }

        Assert.True(_fixture.CodeHost.Files.Any(f => f.Key.Path.StartsWith("docs/prd-")),
            "PRD should be committed to the fake code host");

        Assert.Single(_fixture.LanguageModel.Calls.Where(c => c.SystemPrompt.Contains("PRD Agent")));

        // --- Act 2: Approve the PRD ---
        // This should trigger the orchestrator → Architect Agent automatically
        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            var approved = await approve.ExecuteAsync(featureId, prdArtifactId, "pm@company.com");

            Assert.Equal("pm@company.com", approved.ApprovedBy);
            Assert.NotNull(approved.ApprovedAt);
        }

        // --- Assert 2: Full chain fired ---
        // Feature should have advanced: PrdPending → PrdApproved → ArchPending (orchestrator triggered Architect Agent)
        using (var scope = _fixture.CreateScope())
        {
            var featureRepo = scope.ServiceProvider.GetRequiredService<IFeatureRepository>();
            var feature = await featureRepo.GetByIdAsync(featureId);

            Assert.NotNull(feature);
            Assert.Equal(FeatureStatus.ArchPending, feature!.Status);
        }

        // Architect Agent was invoked
        Assert.Single(_fixture.LanguageModel.Calls.Where(c => c.SystemPrompt.Contains("Architect Agent")));

        // ADR was committed to fake repo
        Assert.True(_fixture.CodeHost.Files.Any(f => f.Key.Path.StartsWith("docs/adr-")),
            "ADR should be committed to the fake code host");

        // ADR artifact was recorded
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            var adrArtifact = await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Adr);

            Assert.NotNull(adrArtifact);
            Assert.Contains("adr-", adrArtifact!.RepositoryPath);
        }

        // Pipeline events were recorded
        using (var scope = _fixture.CreateScope())
        {
            var eventRepo = scope.ServiceProvider.GetRequiredService<IPipelineEventRepository>();
            var events = await eventRepo.GetByFeatureIdAsync(featureId);

            // Expected transitions:
            // 1. Ideation → PrdPending (generate PRD)
            // 2. PrdPending → PrdApproved (approve PRD)
            // 3. PrdApproved → ArchPending (orchestrator triggers Architect Agent)
            Assert.Equal(3, events.Count);
            Assert.Equal(FeatureStatus.Ideation, events[0].FromStatus);
            Assert.Equal(FeatureStatus.PrdPending, events[0].ToStatus);
            Assert.Equal(FeatureStatus.PrdPending, events[1].FromStatus);
            Assert.Equal(FeatureStatus.PrdApproved, events[1].ToStatus);
            Assert.Equal(FeatureStatus.PrdApproved, events[2].FromStatus);
            Assert.Equal(FeatureStatus.ArchPending, events[2].ToStatus);
        }
    }

    [Fact]
    public async Task FullPipeline_IdeationThroughTasksPending_WorksEndToEnd()
    {
        // --- Arrange: Create a project and feature ---
        Guid projectId;
        Guid featureId;

        using (var scope = _fixture.CreateScope())
        {
            var projectManager = scope.ServiceProvider.GetRequiredService<ProjectManager>();
            var project = await projectManager.CreateAsync(
                "Task Pipeline Project",
                "A project for task agent testing",
                "project:\n  name: task-test\nstack:\n  backend: .NET",
                "test-owner/task-test-repo");
            projectId = project.Id;

            var featureManager = scope.ServiceProvider.GetRequiredService<FeatureManager>();
            var feature = await featureManager.CreateAsync(projectId, "Add task management");
            featureId = feature.Id;
        }

        // --- Act 1: Generate PRD ---
        Guid prdArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var generatePrd = scope.ServiceProvider.GetRequiredService<InvokePrdAgentUseCase>();
            var prdArtifact = await generatePrd.ExecuteAsync(featureId);
            prdArtifactId = prdArtifact.Id;
        }

        // --- Act 2: Approve PRD → triggers Architect Agent ---
        Guid adrArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, prdArtifactId, "pm@company.com");
        }

        // Get the ADR artifact ID
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            var adrArtifact = await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Adr);
            Assert.NotNull(adrArtifact);
            adrArtifactId = adrArtifact!.Id;
        }

        // --- Act 3: Approve ADR → triggers Task Agent ---
        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, adrArtifactId, "tech-lead@company.com");
        }

        // --- Assert: Full chain fired through Task Agent ---
        // Feature should be in TasksPending
        using (var scope = _fixture.CreateScope())
        {
            var featureRepo = scope.ServiceProvider.GetRequiredService<IFeatureRepository>();
            var feature = await featureRepo.GetByIdAsync(featureId);

            Assert.NotNull(feature);
            Assert.Equal(FeatureStatus.TasksPending, feature!.Status);
        }

        // Task Agent was invoked
        Assert.Single(_fixture.LanguageModel.Calls.Where(c => c.SystemPrompt.Contains("Task Agent")));

        // Task summary was committed to fake repo
        Assert.True(_fixture.CodeHost.Files.Any(f => f.Key.Path.StartsWith("docs/tasks-")),
            "Task breakdown should be committed to the fake code host");

        // Task artifact was recorded
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            var taskArtifact = await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Task);

            Assert.NotNull(taskArtifact);
            Assert.Contains("tasks-", taskArtifact!.RepositoryPath);
        }

        // GitHub Issues were created (3 tasks in the fake response)
        Assert.Equal(3, _fixture.CodeHost.CreatedIssues.Count(i => i.Repo == "test-owner/task-test-repo"));

        // TaskItems were persisted in DB
        using (var scope = _fixture.CreateScope())
        {
            var taskItemRepo = scope.ServiceProvider.GetRequiredService<ITaskItemRepository>();
            var tasks = await taskItemRepo.GetByFeatureIdAsync(featureId);

            Assert.Equal(3, tasks.Count);
            Assert.Contains(tasks, t => t.AgentType == AgentType.DB);
            Assert.Contains(tasks, t => t.AgentType == AgentType.BE);
            Assert.Contains(tasks, t => t.AgentType == AgentType.FE);
            Assert.All(tasks, t =>
            {
                Assert.NotNull(t.ExternalIssueId);
                Assert.Equal(TaskItemStatus.Pending, t.Status);
            });
        }

        // Pipeline events were recorded (5 transitions total)
        using (var scope = _fixture.CreateScope())
        {
            var eventRepo = scope.ServiceProvider.GetRequiredService<IPipelineEventRepository>();
            var events = await eventRepo.GetByFeatureIdAsync(featureId);

            // 1. Ideation → PrdPending
            // 2. PrdPending → PrdApproved
            // 3. PrdApproved → ArchPending
            // 4. ArchPending → ArchApproved
            // 5. ArchApproved → TasksPending
            Assert.Equal(5, events.Count);
            Assert.Equal(FeatureStatus.ArchApproved, events[4].FromStatus);
            Assert.Equal(FeatureStatus.TasksPending, events[4].ToStatus);
        }
    }

    [Fact]
    public async Task ApproveArtifact_WrongStatus_Throws()
    {
        Guid featureId;
        Guid artifactId;

        // Create a feature still in Ideation and manually add an artifact
        using (var scope = _fixture.CreateScope())
        {
            var projectManager = scope.ServiceProvider.GetRequiredService<ProjectManager>();
            var project = await projectManager.CreateAsync(
                "Wrong Status Project", "test", "config: test", "test/wrong-status");

            var featureManager = scope.ServiceProvider.GetRequiredService<FeatureManager>();
            var feature = await featureManager.CreateAsync(project.Id, "Some feature");
            featureId = feature.Id;

            // Manually add a PRD artifact (simulating it exists but feature is still in Ideation)
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            var artifact = Artifact.Create(featureId, ArtifactType.Prd, "docs/prd-test.md");
            await artifactRepo.AddAsync(artifact);
            artifactId = artifact.Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();

            // Feature is in Ideation, but PRD approval requires PrdPending
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => approve.ExecuteAsync(featureId, artifactId, "user@test.com"));

            Assert.Contains("PrdPending", ex.Message);
        }
    }

    [Fact]
    public async Task ApproveArtifact_AlreadyApproved_Throws()
    {
        Guid featureId;
        Guid prdArtifactId;

        // Create project + feature + generate PRD
        using (var scope = _fixture.CreateScope())
        {
            var projectManager = scope.ServiceProvider.GetRequiredService<ProjectManager>();
            var project = await projectManager.CreateAsync(
                "Double Approve Project", "test", "config: test", "test/double-approve");

            var featureManager = scope.ServiceProvider.GetRequiredService<FeatureManager>();
            var feature = await featureManager.CreateAsync(project.Id, "Double approve test");
            featureId = feature.Id;

            var generatePrd = scope.ServiceProvider.GetRequiredService<InvokePrdAgentUseCase>();
            var prd = await generatePrd.ExecuteAsync(featureId);
            prdArtifactId = prd.Id;
        }

        // First approval — should succeed
        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, prdArtifactId, "pm@company.com");
        }

        // Second approval — should fail (already approved)
        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => approve.ExecuteAsync(featureId, prdArtifactId, "another@company.com"));

            Assert.Contains("already been approved", ex.Message);
        }
    }
}
