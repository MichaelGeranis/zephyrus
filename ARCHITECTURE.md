# Zephyrus — Technical Architecture

This document is the complete technical reference for how Zephyrus is built.
For business rules and domain logic, see [BUSINESS.md](BUSINESS.md).

---

## Clean Architecture

Zephyrus follows Clean Architecture (Robert C. Martin) with strict layer separation.

```
┌─────────────────────────────────────┐
│         Frameworks & Drivers        │  ← Web, DB, GitHub, Claude API
│  ┌───────────────────────────────┐  │
│  │     Interface Adapters        │  │  ← Controllers, API, Presenters
│  │  ┌─────────────────────────┐  │  │
│  │  │   Use Cases/Managers    │  │  │  ← Application business rules
│  │  │  ┌───────────────────┐  │  │  │
│  │  │  │     Entities      │  │  │  │  ← Core domain, state machine
│  │  │  └───────────────────┘  │  │  │
│  │  └─────────────────────────┘  │  │
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘
```

**Principles:**
- **Separation of Concerns**: Business rules (inner layers) are isolated from infrastructure details (outer layers).
- **The Dependency Rule**: Inner layers know nothing about outer layers. Dependencies always point inward.
- **Independent of Frameworks/UI/DB**: Core business logic is not bound to external tools.
- **Testable**: Business rules can be tested without UI, database, or web server.
- **SOLID Principles**: Applied throughout for maintainability and flexibility.

---

## The Four Layers

```
Zephyrus.Core              ← Entities (innermost — no dependencies)
Zephyrus.Application       ← Use Cases (depends on Core only)
Zephyrus.Infrastructure    ← Frameworks & Drivers (implements Core interfaces)
Zephyrus.Api               ← Interface Adapters (thin HTTP layer, wires DI)
```

**The Dependency Rule: arrows always point inward. Never outward.**

```
Api → Application → Core ← Infrastructure
```

`Infrastructure` and `Api` both depend on `Core`.
`Infrastructure` never depends on `Application`.
`Application` never depends on `Infrastructure`.
`Core` depends on nothing.

---

## Layer Responsibilities

### Zephyrus.Core — Entities Layer

**What belongs here:**
- Domain entities: `Feature`, `Project`, etc.
- Interfaces: `IFeatureRepository`, `IProjectRepository`, etc.
- Enums: `FeatureStatus`, `ArtifactType`, etc.
- State machine: `PipelineStateMachine`
- Domain exceptions: `InvalidTransitionException`, `ArtifactNotFoundException`, etc.
- Agent input/output record types: `PrdAgentInput`, `PrdAgentOutput`, etc.

**What does NOT belong here:**
- Any NuGet package reference (zero external dependencies)
- Any EF Core, Octokit, HttpClient, or Hangfire references
- Any ASP.NET Core references

**Enforce with:** `<PackageReference>` count in .csproj must stay at zero.

### Zephyrus.Application — Use Cases Layer/Application business rules

**What belongs here:**
- Product Use cases: `InvokePrdAgentUseCase`, `ApproveArtifactUseCase`, `AdvancePipelineUseCase`, etc.
- Managers/Services:  `FeatureManager`, etc.
- Orchestrator logic (calls Core interfaces only — never Infrastructure directly)
- Pipeline event handlers
- Agent coordination logic
- Application-level exceptions: `PipelineConflictException`, `UnauthorizedApprovalException`, etc.

**What does NOT belong here:**
- EF Core, Octokit, HttpClient references
- Controller or HTTP concerns
- Direct instantiation of Infrastructure classes

**Dependency rule check:** Application .csproj references only `Zephyrus.Core`.

### Zephyrus.Infrastructure — Frameworks & Drivers Layer

**What belongs here:**
- EF Core: `ZephyrusDbContext`, migrations, repository implementations
- Octokit.net: `GitHubCodeHost` implementing `ICodeHost`
- Claude API: `ClaudeLanguageModel` implementing `ILanguageModel`
- Hangfire: job queue wiring and background service implementations
- Agent implementations: `PrdAgent`, `ArchitectAgent`, etc. (the actual Claude API calls)

**What does NOT belong here:**
- Business logic or pipeline rules
- Use case orchestration
- References to `Zephyrus.Application`

**Subfolders:**
```
Zephyrus.Infrastructure/
  Persistence/        ← EF Core DbContext, migrations, repositories
  GitHub/             ← Octokit.net implementations
  AI/                 ← Claude API HttpClient wrapper, agent implementations
  Jobs/               ← Hangfire job definitions
```

### Zephyrus.Api — Interface Adapters Layer (thin)

**What belongs here:**
- ASP.NET Core controllers (thin — delegate immediately managers)
- Request/Response DTOs
- DI container registration (wires Infrastructure implementations to Core interfaces)
- Middleware: auth, error handling, logging
- `Program.cs` / startup

**What does NOT belong here:**
- Business logic of any kind
- Direct repository calls
- Direct agent invocations
- State machine logic

**Controller rule:** Every controller action must be ≤15 lines. If it's longer, logic belongs in Application.

### Zephyrus.Web — Presentation Layer

- Next.js (TypeScript) + Tailwind CSS
- Talks to Zephyrus.Api over REST
- **Thin presentation layer — no business logic, no validation, no state transitions**
- All validation, state transitions, and orchestration happen server-side in the .NET backend

---

## Dependency Map

```
Zephyrus.Api
  └── depends on → Zephyrus.Application
                      └── depends on → Zephyrus.Core
                                          └── depends on → nothing

Zephyrus.Infrastructure
  └── depends on → Zephyrus.Core (implements its interfaces)
  └── registered in → Zephyrus.Api (DI container)

Zephyrus.Web
  └── talks to → Zephyrus.Api (over REST)
```

## Data Model — Full Field Definitions

```
Project
  id              UUID, PK
  name            string
  description     string
  config          jsonb          ← The Project Constitution (YAML stored as text)
  github_repo     string         ← "owner/repo"
  created_at      timestamp

Feature
  id              UUID, PK
  project_id      UUID, FK → Project
  prompt          text           ← Original idea input
  status          enum           ← FeatureStatus (see state machine below)
  created_at      timestamp

Artifact
  id              UUID, PK
  feature_id      UUID, FK → Feature
  type            enum           ← ArtifactType: Prd | Adr | Task | Pr | Test
  github_path     string         ← Path in repo (e.g., docs/prd-my-feature.md)
  approved_by     string         ← User identifier (null until approved)
  approved_at     timestamp      ← (null until approved)

TaskItem
  id              UUID, PK
  feature_id      UUID, FK → Feature
  github_issue_id int            ← GitHub Issue number
  title           string
  status          enum           ← TaskItemStatus: Pending | InProgress | PrOpen | Done
  pr_id           int            ← GitHub PR number (null until code generated)
  agent_type      enum           ← AgentType: BE | FE | DB | DevOps

PipelineEvent
  id              UUID, PK
  feature_id      UUID, FK → Feature
  from_status     enum           ← FeatureStatus
  to_status       enum           ← FeatureStatus
  triggered_by    string         ← User identifier or "system"
  timestamp       timestamp

Deployment
  id              UUID, PK
  feature_id      UUID, FK → Feature
  sha             string         ← Git commit SHA
  environment     string         ← e.g., "production", "staging"
  deployed_at     timestamp
  status          enum           ← DeploymentStatus: Pending | Success | Failed
```

---

## State Machine

The state machine lives exclusively in `Zephyrus.Core/Pipeline/PipelineStateMachine.cs`.

### Valid Transitions

```
Ideation         → PrdPending
PrdPending       → PrdApproved
PrdApproved      → ArchPending
ArchPending      → ArchApproved
ArchApproved     → TasksPending
TasksPending     → TasksApproved
TasksApproved    → Coding
Coding           → QaPending
QaPending        → QaApproved
QaApproved       → Deployed
```

**Rules:**
- Any transition not in this list throws `InvalidTransitionException`
- The Orchestrator calls `PipelineStateMachine.Next()` — it never sets status directly
- Status is only persisted after a successful transition validation
- Every transition is logged as a `PipelineEvent`
- Transitions are forward-only. No rollback.

---

## Orchestrator Design

The Orchestrator is a **deterministic state machine — not an AI**. It:

1. Listens for approval events from the UI
2. Listens for GitHub webhook events (PR merged, CI passed/failed)
3. Builds the correct context bundle for the next agent
4. Invokes the next agent asynchronously via the job queue
5. Persists state transitions to `PipelineEvent`

```csharp
// Pseudocode — the core orchestration logic
public async Task OnArtifactApproved(Guid featureId, ArtifactType type)
{
    var feature = await _repo.GetFeatureAsync(featureId);
    var nextStatus = _stateMachine.GetNextStatus(feature.Status);

    if (await AllDependenciesMet(feature, nextStatus))
    {
        await _jobQueue.EnqueueAsync(new AgentJob(featureId, nextStatus));
        await _repo.UpdateStatusAsync(featureId, nextStatus);
    }
}
```

**Key rule:** Agents are stateless. The Orchestrator is stateful. Never put state inside an agent.

---

## Agent Design Pattern

### Contract

Every agent implements this interface from `Zephyrus.Core`:

```csharp
public interface IAgent<TInput, TOutput>
    where TInput : class
    where TOutput : class
{
    Task<TOutput> RunAsync(TInput input, CancellationToken ct = default);
}
```

### Agent Implementation Rules

- Lives in `Zephyrus.Infrastructure/AI/Agents/`
- Input and Output are `record` types defined in `Zephyrus.Core/Agents/`
- System prompt loaded from `/prompts/{agentname}.md` — never hardcoded
- All Claude API calls go through `ILanguageModel` — never raw HttpClient in agent
- Commits artifact to GitHub via `ICodeHost` — never Octokit directly in agent
- Must have a corresponding unit test with mocked `ILanguageModel`
- Must be stateless — no instance fields that change between calls

### Agent Execution Sequence

1. Loads the appropriate system prompt for that agent type
2. Assembles context: Project Constitution + relevant artifacts + (optional) existing code
3. Calls Claude API via `ILanguageModel`
4. Parses structured output (JSON or Markdown depending on agent)
5. Commits artifact to GitHub via `ICodeHost`
6. Returns result to Orchestrator

### Adding a New Agent

1. Define `{Agent}Input` and `{Agent}Output` in `Zephyrus.Core/Agents/`
2. Create `{Agent}Agent : IAgent<TInput, TOutput>` in `Zephyrus.Infrastructure/AI/Agents/`
3. Create `Invoke{Agent}AgentUseCase` in `Zephyrus.Application/UseCases/`
4. Wire into `PipelineOrchestrator` for the appropriate approval trigger
5. Register in `DependencyInjection.cs` (both Application and Infrastructure)
6. Add integration tests

### Retry Policy

Agents can be retried freely — they are stateless. On failure, the agent is retried once with the failure report injected as additional context. After one retry, escalate to human.

---

## Project Constitution

The Project Constitution is a YAML config file the Tech Lead fills out once per project. **Every agent reads it before acting.** It is the context spine — the baton passed between all agents.

```yaml
project:
  name: my-app
  description: "Brief description of what this project does"

stack:
  frontend: "Next.js + TypeScript"
  backend: "ASP.NET Core + C#"
  database: "PostgreSQL"
  orm: "Entity Framework Core"

conventions:
  linting: "ESLint + Prettier (frontend), dotnet format (backend)"
  testing: "Jest (frontend), xUnit (backend)"
  branch_strategy: "feature branches → main"
  commit_style: "conventional commits"
  pr_template: true

deployment:
  target: "Railway"
  trigger: "merge to main"
  environment_variables:
    - DATABASE_URL
    - CLAUDE_API_KEY
    - GITHUB_TOKEN

architecture:
  patterns:
    - "Repository pattern for data access"
    - "CQRS for API handlers"
    - "Stateless agents — all state in DB and GitHub"
  api_style: "REST + JSON"
  auth: "JWT"
```