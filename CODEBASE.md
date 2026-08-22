# Zephyrus — Codebase Map

This file describes the project structure. It is read by AI agents to understand
what exists before generating code. Engineers and AI must keep it up to date.

## Architecture

Clean Architecture with four .NET layers + Next.js frontend:

```
Core (entities, interfaces, enums) → zero dependencies
Application (use cases, managers) → depends on Core only
Infrastructure (EF Core, GitHub, Claude API) → implements Core interfaces
Api (controllers, DI, middleware) → thin HTTP layer
Web (Next.js + TypeScript) → presentation only, calls API
```

## Project Structure

```
src/
├── Zephyrus.Core/
│   ├── Agents/
│   │   ├── ArchitectAgentInput.cs          — Input record for Architect Agent
│   │   ├── ArchitectAgentOutput.cs         — Output record for Architect Agent
│   │   ├── CodeAgentInput.cs               — Input record for Code Agent (multi-pass)
│   │   ├── CodeAgentOutput.cs              — Output record for Code Agent (action-based)
│   │   ├── ConversationMessage.cs          — Message record for multi-turn LLM conversations
│   │   ├── DevOpsAgentInput.cs             — Input record for DevOps Agent
│   │   ├── DevOpsAgentOutput.cs            — Output record for DevOps Agent
│   │   ├── PrdAgentInput.cs                — Input record for PRD Agent
│   │   ├── PrdAgentOutput.cs               — Output record for PRD Agent
│   │   ├── QaAgentInput.cs                 — Input record for QA Agent
│   │   ├── QaAgentOutput.cs                — Output record for QA Agent
│   │   ├── TaskAgentInput.cs               — Input record for Task Agent
│   │   └── TaskAgentOutput.cs              — Output record for Task Agent
│   ├── Entities/
│   │   ├── AgentInvocation.cs              — Audit log of agent API calls
│   │   ├── Artifact.cs                     — Output produced by an agent (PRD, ADR, PR, etc.)
│   │   ├── Deployment.cs                   — Deployment record (sha, environment, status)
│   │   ├── Feature.cs                      — Unit of work moving through the pipeline
│   │   ├── PipelineEvent.cs                — Audit log of state transitions
│   │   ├── Project.cs                      — Software project with constitution and repo slug
│   │   └── TaskItem.cs                     — Atomic work unit assigned to Code Agent
│   ├── Enums/
│   │   ├── AgentType.cs                    — BE, FE, DB, DevOps
│   │   ├── ArtifactType.cs                 — Prd, Adr, Task, Pr, Test
│   │   ├── DeploymentStatus.cs             — Pending, Success, Failed
│   │   ├── FeatureStatus.cs                — Pipeline statuses (Ideation → Deployed)
│   │   └── TaskStatus.cs                   — Pending, InProgress, PrOpen, Done
│   ├── Exceptions/
│   │   ├── ArtifactNotFoundException.cs    — Thrown when artifact not found
│   │   └── InvalidTransitionException.cs   — Thrown on invalid state machine transition
│   ├── Interfaces/
│   │   ├── IAgentInvocationRepository.cs   — CRUD for AgentInvocation
│   │   ├── IAgentJobDispatcher.cs          — Routes a queued AgentJob to its use case
│   │   ├── IAgentRunner.cs                 — Generic agent execution interface
│   │   ├── IArtifactRepository.cs          — CRUD for Artifact
│   │   ├── ICodeHost.cs                    — GitHub operations (branches, files, PRs, issues)
│   │   ├── ICodeHostFactory.cs             — Factory for per-project ICodeHost instances
│   │   ├── IDeploymentRepository.cs        — CRUD for Deployment
│   │   ├── IFeatureRepository.cs           — CRUD for Feature
│   │   ├── IJobQueue.cs                    — Queues agent work for background execution
│   │   ├── ILanguageModel.cs               — Claude API abstraction (single + multi-turn)
│   │   ├── IPipelineEventRepository.cs     — CRUD for PipelineEvent
│   │   ├── IProjectRepository.cs           — CRUD for Project
│   │   ├── IPromptLoader.cs                — Load system prompts from files
│   │   └── ITaskItemRepository.cs          — CRUD for TaskItem
│   ├── Jobs/
│   │   ├── AgentJob.cs                     — A queued unit of agent work (feature + kind)
│   │   └── AgentJobKind.cs                 — Architect, Task, Code, Qa, DevOps
│   └── Pipeline/
│       └── PipelineStateMachine.cs         — Enforces valid feature status transitions
│
├── Zephyrus.Application/
│   ├── DependencyInjection.cs              — Registers Application services
│   ├── Managers/
│   │   ├── ArtifactManager.cs              — Artifact query operations
│   │   ├── FeatureManager.cs               — Feature CRUD and listing
│   │   └── ProjectManager.cs               — Project CRUD and listing
│   ├── Orchestration/
│   │   ├── AgentJobDispatcher.cs           — Runs a queued AgentJob via the matching use case
│   │   └── PipelineOrchestrator.cs         — Queues the next agent job on approval
│   └── UseCases/
│       ├── ApproveArtifactUseCase.cs       — Approve artifact and advance pipeline
│       ├── InvokeArchitectAgentUseCase.cs  — Orchestrate ADR generation
│       ├── InvokeCodeAgentUseCase.cs       — Orchestrate code generation (multi-pass loop)
│       ├── InvokeDevOpsAgentUseCase.cs     — Orchestrate CI/CD config generation
│       ├── InvokePrdAgentUseCase.cs        — Orchestrate PRD generation
│       ├── InvokeQaAgentUseCase.cs         — Orchestrate test generation
│       ├── InvokeTaskAgentUseCase.cs       — Orchestrate task breakdown
│       ├── HandleDeploymentStatusUseCase.cs — Deployment result → Deployed
│       ├── HandlePullRequestClosedUseCase.cs — Merged PR → task done + pending deployment
│       ├── RerunStepUseCase.cs             — Re-invoke agent for a failed step
│       ├── RetryArtifactCommitUseCase.cs   — Retry failed GitHub commits
│       └── UpdateArtifactContentUseCase.cs — Edit artifact content before commit
│
├── Zephyrus.Infrastructure/
│   ├── DependencyInjection.cs              — Registers Infrastructure services
│   ├── AI/
│   │   ├── Agents/
│   │   │   ├── ArchitectAgent.cs           — ADR generation via Claude
│   │   │   ├── CodeAgent.cs                — Code generation via Claude (multi-pass)
│   │   │   ├── DevOpsAgent.cs              — CI/CD config generation via Claude
│   │   │   ├── PrdAgent.cs                 — PRD generation via Claude
│   │   │   ├── QaAgent.cs                  — Test generation via Claude
│   │   │   └── TaskAgent.cs                — Task breakdown via Claude
│   │   ├── ClaudeLanguageModel.cs          — ILanguageModel implementation (Anthropic API)
│   │   ├── ClaudeLanguageModelOptions.cs   — Configuration options (model, key, max tokens)
│   │   └── FilePromptLoader.cs             — Loads prompts from prompts/ directory
│   ├── Jobs/
│   │   ├── AgentJobWorker.cs               — BackgroundService draining the queue, one DI scope per job
│   │   ├── BackgroundJobQueue.cs           — In-process IJobQueue backed by a channel
│   │   └── InlineJobQueue.cs               — Runs jobs inline (tests only)
│   ├── GitHub/
│   │   ├── GitHubCodeHost.cs               — ICodeHost implementation (Octokit.net)
│   │   └── GitHubCodeHostFactory.cs        — Creates GitHubCodeHost per project token
│   └── Persistence/
│       ├── Configurations/                 — EF Core entity type configurations
│       │   ├── AgentInvocationConfiguration.cs
│       │   ├── ArtifactConfiguration.cs
│       │   ├── DeploymentConfiguration.cs
│       │   ├── FeatureConfiguration.cs
│       │   ├── PipelineEventConfiguration.cs
│       │   ├── ProjectConfiguration.cs
│       │   └── TaskItemConfiguration.cs
│       ├── Migrations/                     — EF Core migrations
│       ├── Repositories/
│       │   ├── AgentInvocationRepository.cs
│       │   ├── ArtifactRepository.cs
│       │   ├── FeatureRepository.cs
│       │   ├── PipelineEventRepository.cs
│       │   ├── ProjectRepository.cs
│       │   └── TaskItemRepository.cs
│       └── ZephyrusDbContext.cs            — EF Core DbContext
│
├── Zephyrus.Api/
│   ├── Program.cs                          — App startup and DI wiring
│   ├── Controllers/
│   │   ├── ArtifactsController.cs          — Artifact endpoints (approve, edit, retry)
│   │   ├── FeaturesController.cs           — Feature endpoints (CRUD, invoke agents)
│   │   └── ProjectsController.cs          — Project endpoints (CRUD)
│   └── Middleware/
│       └── ExceptionHandlingMiddleware.cs  — Global error handler
│
└── Zephyrus.Web/                           — Next.js 15 + TypeScript + Tailwind CSS 4
    ├── app/
    │   ├── layout.tsx                      — Root layout
    │   ├── page.tsx                        — Landing page
    │   ├── globals.css                     — Global styles
    │   ├── global-error.tsx                — Error boundary
    │   ├── dashboard/page.tsx              — Dashboard view
    │   ├── projects/
    │   │   ├── page.tsx                    — Project list
    │   │   └── [id]/page.tsx              — Project detail
    │   └── features/
    │       └── [id]/
    │           ├── page.tsx                — Feature detail with pipeline view
    │           ├── artifacts/[artifactId]/page.tsx  — Artifact detail
    │           └── invocations/[invocationId]/page.tsx — Agent invocation detail
    ├── components/
    │   ├── ApprovalGate.tsx                — Approval button component
    │   └── StatusBadge.tsx                 — Pipeline status badge
    └── lib/
        ├── api.ts                          — API client functions
        └── types.ts                        — TypeScript type definitions

prompts/                                    — Agent system prompts
├── architect.md                            — Architect Agent prompt
├── code.md                                 — Code Agent prompt (multi-pass)
├── devops.md                               — DevOps Agent prompt
├── prd.md                                  — PRD Agent prompt
├── qa.md                                   — QA Agent prompt
└── task.md                                 — Task Agent prompt

tests/
├── Zephyrus.UnitTests/
│   ├── Agents/
│   │   ├── ArchitectAgentTests.cs
│   │   ├── CodeAgentTests.cs
│   │   ├── DevOpsAgentTests.cs
│   │   ├── Fakes.cs                       — FakeLanguageModel, FakePromptLoader
│   │   ├── PrdAgentTests.cs
│   │   ├── QaAgentTests.cs
│   │   └── TaskAgentTests.cs
│   ├── ExceptionHandlingMiddlewareTests.cs
│   ├── PipelineStateMachineTests.cs
│   ├── RetryArtifactCommitUseCaseTests.cs
│   └── SlugGenerationTests.cs
└── Zephyrus.IntegrationTests/
    ├── ApiIntegrationTests.cs              — HTTP endpoint tests
    ├── PipelineIntegrationTests.cs         — Full pipeline flow tests
    ├── PipelineFixture.cs                  — Shared test fixture
    ├── ZephyrusApiFactory.cs               — WebApplicationFactory for tests
    └── Fakes/
        ├── FakeCodeHost.cs                 — In-memory ICodeHost
        ├── FakeCodeHostFactory.cs          — Returns shared FakeCodeHost
        ├── FakeLanguageModel.cs            — Canned agent responses
        └── FakePromptLoader.cs             — Returns static prompt content
```
