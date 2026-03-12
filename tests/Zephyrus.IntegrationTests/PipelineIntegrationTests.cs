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

        Assert.Single(_fixture.LanguageModel.Calls, c => c.SystemPrompt.Contains("PRD Agent"));

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
        Assert.Single(_fixture.LanguageModel.Calls, c => c.SystemPrompt.Contains("Architect Agent"));

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
        Assert.Contains(_fixture.LanguageModel.Calls, c => c.SystemPrompt.Contains("Task Agent"));

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
    public async Task FullPipeline_IdeationThroughCoding_WorksEndToEnd()
    {
        // --- Arrange: Create a project and feature ---
        Guid projectId;
        Guid featureId;

        using (var scope = _fixture.CreateScope())
        {
            var projectManager = scope.ServiceProvider.GetRequiredService<ProjectManager>();
            var project = await projectManager.CreateAsync(
                "Code Pipeline Project",
                "A project for code agent testing",
                "project:\n  name: code-test\nstack:\n  backend: .NET",
                "test-owner/code-test-repo");
            projectId = project.Id;

            var featureManager = scope.ServiceProvider.GetRequiredService<FeatureManager>();
            var feature = await featureManager.CreateAsync(projectId, "Add code generation");
            featureId = feature.Id;
        }

        // --- Run through PRD → Approve → Architect → Approve → Tasks → Approve ---
        Guid prdArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var generatePrd = scope.ServiceProvider.GetRequiredService<InvokePrdAgentUseCase>();
            var prdArtifact = await generatePrd.ExecuteAsync(featureId);
            prdArtifactId = prdArtifact.Id;
        }

        // Approve PRD → triggers Architect Agent
        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, prdArtifactId, "pm@company.com");
        }

        // Approve ADR → triggers Task Agent
        Guid adrArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            var adrArtifact = await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Adr);
            Assert.NotNull(adrArtifact);
            adrArtifactId = adrArtifact!.Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, adrArtifactId, "tech-lead@company.com");
        }

        // Approve Tasks → triggers Code Agents
        Guid taskArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            var taskArtifact = await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Task);
            Assert.NotNull(taskArtifact);
            taskArtifactId = taskArtifact!.Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, taskArtifactId, "tech-lead@company.com");
        }

        // --- Assert: Feature is now in Coding status ---
        using (var scope = _fixture.CreateScope())
        {
            var featureRepo = scope.ServiceProvider.GetRequiredService<IFeatureRepository>();
            var feature = await featureRepo.GetByIdAsync(featureId);

            Assert.NotNull(feature);
            Assert.Equal(FeatureStatus.Coding, feature!.Status);
        }

        // Code Agent was invoked (once per task — 3 tasks in fake response)
        Assert.True(
            _fixture.LanguageModel.Calls.Count(c => c.SystemPrompt.Contains("Code Agent")) >= 3,
            "Code Agent should be invoked at least 3 times (once per task)");

        // Feature branches were created
        Assert.Equal(3, _fixture.CodeHost.CreatedBranches.Count(b => b.StartsWith("feature/add-code-generation/")));

        // PRs were created (one per task)
        Assert.Equal(3, _fixture.CodeHost.CreatedPrs.Count(p => p.Repo == "test-owner/code-test-repo"));

        // Files were committed to feature branches
        Assert.True(_fixture.CodeHost.Files.Any(f => f.Key.Path == "src/Example.cs"),
            "Code files should be committed to the fake code host");

        // All TaskItems should have PRs linked and be in PrOpen status
        using (var scope = _fixture.CreateScope())
        {
            var taskItemRepo = scope.ServiceProvider.GetRequiredService<ITaskItemRepository>();
            var tasks = await taskItemRepo.GetByFeatureIdAsync(featureId);

            Assert.Equal(3, tasks.Count);
            Assert.All(tasks, t =>
            {
                Assert.NotNull(t.PrId);
                Assert.Equal(TaskItemStatus.PrOpen, t.Status);
            });
        }

        // Pr artifact was recorded
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            var prArtifact = await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Pr);

            Assert.NotNull(prArtifact);
        }

        // Pipeline events: 7 transitions total
        using (var scope = _fixture.CreateScope())
        {
            var eventRepo = scope.ServiceProvider.GetRequiredService<IPipelineEventRepository>();
            var events = await eventRepo.GetByFeatureIdAsync(featureId);

            // 1. Ideation → PrdPending
            // 2. PrdPending → PrdApproved
            // 3. PrdApproved → ArchPending
            // 4. ArchPending → ArchApproved
            // 5. ArchApproved → TasksPending
            // 6. TasksPending → TasksApproved
            // 7. TasksApproved → Coding
            Assert.Equal(7, events.Count);
            Assert.Equal(FeatureStatus.TasksApproved, events[6].FromStatus);
            Assert.Equal(FeatureStatus.Coding, events[6].ToStatus);
        }
    }

    [Fact]
    public async Task FullPipeline_IdeationThroughQaPending_WorksEndToEnd()
    {
        // --- Arrange ---
        Guid projectId;
        Guid featureId;

        using (var scope = _fixture.CreateScope())
        {
            var projectManager = scope.ServiceProvider.GetRequiredService<ProjectManager>();
            var project = await projectManager.CreateAsync(
                "QA Pipeline Project",
                "A project for QA agent testing",
                "project:\n  name: qa-test\nstack:\n  backend: .NET",
                "test-owner/qa-test-repo");
            projectId = project.Id;

            var featureManager = scope.ServiceProvider.GetRequiredService<FeatureManager>();
            var feature = await featureManager.CreateAsync(projectId, "Add qa validation");
            featureId = feature.Id;
        }

        // --- Run through PRD → Architect → Tasks → Code pipeline ---
        Guid prdArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var generatePrd = scope.ServiceProvider.GetRequiredService<InvokePrdAgentUseCase>();
            prdArtifactId = (await generatePrd.ExecuteAsync(featureId)).Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, prdArtifactId, "pm@company.com");
        }

        Guid adrArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            adrArtifactId = (await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Adr))!.Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, adrArtifactId, "tech-lead@company.com");
        }

        Guid taskArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            taskArtifactId = (await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Task))!.Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, taskArtifactId, "tech-lead@company.com");
        }

        // Feature should now be in Coding (Code Agent has run)
        using (var scope = _fixture.CreateScope())
        {
            var featureRepo = scope.ServiceProvider.GetRequiredService<IFeatureRepository>();
            var feature = await featureRepo.GetByIdAsync(featureId);
            Assert.Equal(FeatureStatus.Coding, feature!.Status);
        }

        // --- Act: Approve Pr artifact → triggers QA Agent ---
        Guid prArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            prArtifactId = (await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Pr))!.Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, prArtifactId, "tech-lead@company.com");
        }

        // --- Assert: Feature is now in QaPending, QA Agent has run ---
        using (var scope = _fixture.CreateScope())
        {
            var featureRepo = scope.ServiceProvider.GetRequiredService<IFeatureRepository>();
            var feature = await featureRepo.GetByIdAsync(featureId);

            Assert.NotNull(feature);
            Assert.Equal(FeatureStatus.QaPending, feature!.Status);
        }

        // QA Agent was invoked
        Assert.Contains(_fixture.LanguageModel.Calls, c => c.SystemPrompt.Contains("QA Agent"));

        // QA report was committed
        Assert.True(_fixture.CodeHost.Files.Any(f => f.Key.Path.StartsWith("docs/qa-report-")),
            "QA report should be committed to the fake code host");

        // Test files were committed
        Assert.True(_fixture.CodeHost.Files.Any(f => f.Key.Path.Contains("tests/")),
            "Test files should be committed to the fake code host");

        // Test artifact was recorded
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            var testArtifact = await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Test);

            Assert.NotNull(testArtifact);
            Assert.Contains("qa-report-", testArtifact!.RepositoryPath);
        }

        // Pipeline events: 8 transitions
        using (var scope = _fixture.CreateScope())
        {
            var eventRepo = scope.ServiceProvider.GetRequiredService<IPipelineEventRepository>();
            var events = await eventRepo.GetByFeatureIdAsync(featureId);

            // 1. Ideation → PrdPending
            // 2. PrdPending → PrdApproved
            // 3. PrdApproved → ArchPending
            // 4. ArchPending → ArchApproved
            // 5. ArchApproved → TasksPending
            // 6. TasksPending → TasksApproved
            // 7. TasksApproved → Coding
            // 8. Coding → QaPending (Pr approval)
            Assert.Equal(8, events.Count);
            Assert.Equal(FeatureStatus.Coding, events[7].FromStatus);
            Assert.Equal(FeatureStatus.QaPending, events[7].ToStatus);
        }
    }

    [Fact]
    public async Task FullPipeline_IdeationThroughQaApproved_WorksEndToEnd()
    {
        // --- Arrange ---
        Guid projectId;
        Guid featureId;

        using (var scope = _fixture.CreateScope())
        {
            var projectManager = scope.ServiceProvider.GetRequiredService<ProjectManager>();
            var project = await projectManager.CreateAsync(
                "DevOps Pipeline Project",
                "A project for DevOps agent testing",
                "project:\n  name: devops-test\nstack:\n  backend: .NET\ndeployment:\n  target: Railway",
                "test-owner/devops-test-repo");
            projectId = project.Id;

            var featureManager = scope.ServiceProvider.GetRequiredService<FeatureManager>();
            var feature = await featureManager.CreateAsync(projectId, "Add devops workflow");
            featureId = feature.Id;
        }

        // --- Run through PRD → Architect → Tasks → Code → QA pipeline ---
        Guid prdArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var generatePrd = scope.ServiceProvider.GetRequiredService<InvokePrdAgentUseCase>();
            prdArtifactId = (await generatePrd.ExecuteAsync(featureId)).Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, prdArtifactId, "pm@company.com");
        }

        Guid adrArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            adrArtifactId = (await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Adr))!.Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, adrArtifactId, "tech-lead@company.com");
        }

        Guid taskArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            taskArtifactId = (await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Task))!.Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, taskArtifactId, "tech-lead@company.com");
        }

        // Approve Pr → triggers QA Agent
        Guid prArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            prArtifactId = (await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Pr))!.Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, prArtifactId, "tech-lead@company.com");
        }

        // Feature should now be in QaPending
        using (var scope = _fixture.CreateScope())
        {
            var featureRepo = scope.ServiceProvider.GetRequiredService<IFeatureRepository>();
            var feature = await featureRepo.GetByIdAsync(featureId);
            Assert.Equal(FeatureStatus.QaPending, feature!.Status);
        }

        // --- Act: Approve Test artifact → triggers DevOps Agent ---
        Guid testArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            testArtifactId = (await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Test))!.Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, testArtifactId, "qa@company.com");
        }

        // --- Assert: Feature is now in QaApproved, DevOps Agent has run ---
        using (var scope = _fixture.CreateScope())
        {
            var featureRepo = scope.ServiceProvider.GetRequiredService<IFeatureRepository>();
            var feature = await featureRepo.GetByIdAsync(featureId);

            Assert.NotNull(feature);
            Assert.Equal(FeatureStatus.QaApproved, feature!.Status);
        }

        // DevOps Agent was invoked
        Assert.Single(_fixture.LanguageModel.Calls, c => c.SystemPrompt.Contains("DevOps Agent"));

        // Workflow file was committed
        Assert.True(_fixture.CodeHost.Files.Any(f => f.Key.Path == ".github/workflows/deploy.yml"),
            "Workflow file should be committed to the fake code host");

        // Workflow artifact was recorded
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            var workflowArtifact = await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Workflow);

            Assert.NotNull(workflowArtifact);
            Assert.Equal(".github/workflows/deploy.yml", workflowArtifact!.RepositoryPath);
        }

        // --- Act 2: Approve Workflow artifact → feature advances to Deployed ---
        Guid workflowArtifactId;
        using (var scope = _fixture.CreateScope())
        {
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
            workflowArtifactId = (await artifactRepo.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Workflow))!.Id;
        }

        using (var scope = _fixture.CreateScope())
        {
            var approve = scope.ServiceProvider.GetRequiredService<ApproveArtifactUseCase>();
            await approve.ExecuteAsync(featureId, workflowArtifactId, "tech-lead@company.com");
        }

        // --- Assert: Feature is now Deployed ---
        using (var scope = _fixture.CreateScope())
        {
            var featureRepo = scope.ServiceProvider.GetRequiredService<IFeatureRepository>();
            var feature = await featureRepo.GetByIdAsync(featureId);

            Assert.NotNull(feature);
            Assert.Equal(FeatureStatus.Deployed, feature!.Status);
        }

        // Pipeline events: 10 transitions (full pipeline)
        using (var scope = _fixture.CreateScope())
        {
            var eventRepo = scope.ServiceProvider.GetRequiredService<IPipelineEventRepository>();
            var events = await eventRepo.GetByFeatureIdAsync(featureId);

            // 1. Ideation → PrdPending
            // 2. PrdPending → PrdApproved
            // 3. PrdApproved → ArchPending
            // 4. ArchPending → ArchApproved
            // 5. ArchApproved → TasksPending
            // 6. TasksPending → TasksApproved
            // 7. TasksApproved → Coding
            // 8. Coding → QaPending
            // 9. QaPending → QaApproved
            // 10. QaApproved → Deployed
            Assert.Equal(10, events.Count);
            Assert.Equal(FeatureStatus.QaApproved, events[9].FromStatus);
            Assert.Equal(FeatureStatus.Deployed, events[9].ToStatus);
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
