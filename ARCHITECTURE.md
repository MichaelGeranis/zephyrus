# Zephyrus — Technical Architecture

This document is the complete technical reference for how Zephyrus is built.
For business rules and domain logic, see [BUSINESS_CONCEPTS.md](BUSINESS_CONCEPTS.md).
For Claude Code working context, see [CLAUDE.md](CLAUDE.md).

---

## Clean Architecture

Zephyrus follows Clean Architecture (Robert C. Martin) with strict layer separation.

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
- Domain entities: `Feature`, `Project`, `Artifact`, `TaskItem`, `PipelineEvent`, `Deployment`
- Interfaces: `IFeatureRepository`, `IProjectRepository`, `IArtifactRepository`, `ITaskItemRepository`, `IPipelineEventRepository`, `IDeploymentRepository`, `IAgentRunner`, `ICodeHost`, `ILanguageModel`
- Enums: `FeatureStatus`, `ArtifactType`, `AgentType`, `TaskItemStatus`, `DeploymentStatus`
- State machine: `PipelineStateMachine`
- Domain exceptions: `InvalidTransitionException`, `ArtifactNotFoundException`
- Agent input/output record types: `PrdAgentInput`, `PrdAgentOutput`, etc.

**What does NOT belong here:**
- Any NuGet package reference (zero external dependencies)
- Any EF Core, Octokit, HttpClient, or Hangfire references
- Any ASP.NET Core references

**Enforce with:** `<PackageReference>` count in .csproj must stay at zero.

### Zephyrus.Application — Use Cases Layer

**What belongs here:**
- Use cases: `InvokePrdAgentUseCase`, `ApproveArtifactUseCase`, `AdvancePipelineUseCase`, etc.
- Orchestrator logic (calls Core interfaces only — never Infrastructure directly)
- Pipeline event handlers
- Agent coordination logic
- Application-level exceptions: `PipelineConflictException`, `UnauthorizedApprovalException`

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
- ASP.NET Core controllers (thin — delegate immediately to use cases)
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

```
┌─────────────────────────────────────┐
│         Frameworks & Drivers        │  ← Web, DB, GitHub, Claude API
│  ┌───────────────────────────────┐  │
│  │     Interface Adapters        │  │  ← Controllers, API, Presenters
│  │  ┌─────────────────────────┐  │  │
│  │  │      Use Cases          │  │  │  ← Application business rules
│  │  │  ┌───────────────────┐  │  │  │
│  │  │  │     Entities      │  │  │  │  ← Core domain, state machine
│  │  │  └───────────────────┘  │  │  │
│  │  └─────────────────────────┘  │  │
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘
```

---

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

---

## GitHub Integration Rules

- **GitHub is source of truth for all code artifacts** — never store file contents in the database
- Branch naming: `feature/{feature-slug}/{task-id}`
- PR title format: `[Zephyrus] {task-title} (#{issue-number})`
- All PRs must be linked to their originating GitHub Issue
- All Octokit.net calls live in `Zephyrus.Infrastructure/GitHub/` only
- The rest of the codebase uses `ICodeHost` — never Octokit types directly
- Webhook events drive pipeline state advancement where possible
- Fallback: polling every 60 seconds for missed webhooks

---

## Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| Use cases | `{Verb}{Noun}UseCase` | `ApproveArtifactUseCase` |
| Agents | `{Name}Agent` | `PrdAgent`, `ArchitectAgent` |
| Interfaces (Core) | `I{Name}` | `IFeatureRepository`, `ICodeHost` |
| Repository implementations | `{Name}Repository` | `FeatureRepository` |
| Request DTOs | `{Name}Request` | `ApproveArtifactRequest` |
| Response DTOs | `{Name}Response` | `ArtifactResponse` |
| Domain entities | PascalCase noun | `Feature`, `Artifact` |
| Enums | PascalCase | `FeatureStatus.PrdApproved` |
| EF migrations | `{YYYYMMDD}_{Description}` | `20260310_AddFeatureTable` |
| Agent input records | `{Name}AgentInput` | `PrdAgentInput` |
| Agent output records | `{Name}AgentOutput` | `PrdAgentOutput` |

---

## Ubiquitous Language

These terms must be used consistently across all code, comments, and variable names.

| Term | Meaning | Never use instead |
|------|---------|-------------------|
| `Feature` | A unit of work moving through the pipeline | story, ticket, item, task |
| `Artifact` | An output produced by an agent (PRD, ADR, PR, tests) | document, output, result |
| `Constitution` | The project config file every agent reads | config, settings (except for infra wiring) |
| `Pipeline` | The full sequence of stages for a Feature | workflow, process |
| `ApprovalGate` | A human validation step between stages | checkpoint, review |
| `Agent` | A stateless AI function that takes input and produces an artifact | bot, assistant, worker |
| `Orchestrator` | The deterministic state machine that coordinates agents | coordinator, manager |
| `Task` | An atomic unit of work assigned to the Code Agent | subtask, item, issue |

---

## Testing Rules

- Every use case must have a unit test in `Zephyrus.Tests/Application/`
- Every agent must have a unit test in `Zephyrus.Tests/Infrastructure/AI/`
- Unit tests mock all infrastructure via Core interfaces
- No test may make a real HTTP call or DB call
- Integration tests live in `Zephyrus.IntegrationTests/` with SQLite in-memory + fakes
- Test naming: `{MethodName}_When{Condition}_Should{ExpectedResult}`

### Test Fakes

| Real Service | Test Fake | Behavior |
|-------------|-----------|----------|
| PostgreSQL | SQLite in-memory | Shared connection, schema auto-created |
| GitHub (Octokit) | `FakeCodeHost` | Dictionary-backed file storage |
| Claude API | `FakeLanguageModel` | Returns canned markdown per agent type |

---

## Pre-Commit Checklist

Before finalizing any implementation, verify:

- [ ] No `HttpClient` instantiation outside `Zephyrus.Infrastructure`
- [ ] No `DbContext` reference outside `Zephyrus.Infrastructure/Persistence/`
- [ ] No Octokit types referenced outside `Zephyrus.Infrastructure/GitHub/`
- [ ] No business logic in `Zephyrus.Api` controllers
- [ ] `Zephyrus.Core` has zero `<PackageReference>` entries
- [ ] `Zephyrus.Application` references only `Zephyrus.Core`
- [ ] Every new agent has a corresponding unit test
- [ ] All new entities follow ubiquitous language naming
- [ ] State machine not bypassed — all status changes go through `PipelineStateMachine`

---

## What To Do When Unsure Where Something Goes

1. **Is it a domain rule or entity?** → `Zephyrus.Core`
2. **Is it a use case or orchestration step?** → `Zephyrus.Application`
3. **Is it a call to an external system (DB, GitHub, Claude)?** → `Zephyrus.Infrastructure`
4. **Is it HTTP in/out or DI wiring?** → `Zephyrus.Api`
5. **Still unsure?** → Default to `Zephyrus.Core` and work outward

---

*This document is the complete technical architecture reference.
It takes precedence over CLAUDE.md in case of conflict on structural matters.
Update it whenever a significant architectural decision is made.*
