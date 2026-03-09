# Zephyrus — Architecture Rules

This document is the strict rulebook for all Claude Code sessions.
CLAUDE.md is the project brief. This file is the law.
When in doubt about where something goes, consult this file first.

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

### Zephyrus.Core
**What belongs here:**
- Domain entities: `Feature`, `Project`, `Artifact`, `Task`, `PipelineEvent`, `Deployment`
- Interfaces: `IFeatureRepository`, `IAgentRunner`, `ICodeHost`, `ILanguageModel`
- Enums: `FeatureStatus`, `ArtifactType`, `AgentType`, `TaskStatus`
- State machine: `PipelineStateMachine`
- Domain exceptions: `InvalidTransitionException`, `ArtifactNotFoundException`
- Agent input/output record types: `PrdAgentInput`, `PrdAgentOutput`, etc.

**What does NOT belong here:**
- Any NuGet package reference (zero external dependencies)
- Any EF Core, Octokit, HttpClient, or Hangfire references
- Any ASP.NET Core references
- Any infrastructure concerns whatsoever

**Enforce with:** `<PackageReference>` count in .csproj must stay at zero.

---

### Zephyrus.Application
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

---

### Zephyrus.Infrastructure
**What belongs here:**
- EF Core: `ZephyrusDbContext`, migrations, repository implementations
- Octokit.net: `GitHubCodeHost` implementing `ICodeHost`
- Claude API: `ClaudeLanguageModel` implementing `ILanguageModel`
- Hangfire: job queue wiring and background service implementations
- Agent implementations: `PrdAgent`, `ArchitectAgent`, etc. (the actual Claude API calls)
- External configuration readers

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

---

### Zephyrus.Api
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
Never use synonyms or abbreviations.

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

## Agent Contract

Every agent must implement this interface from `Zephyrus.Core`:

```csharp
public interface IAgent<TInput, TOutput>
    where TInput : class
    where TOutput : class
{
    Task<TOutput> RunAsync(TInput input, CancellationToken ct = default);
}
```

**Rules for every agent implementation:**
- Lives in `Zephyrus.Infrastructure/AI/Agents/`
- Input and Output are `record` types defined in `Zephyrus.Core/Agents/`
- System prompt loaded from `/prompts/{agentname}.md` — never hardcoded
- All Claude API calls go through `ILanguageModel` — never raw HttpClient in agent
- Commits artifact to GitHub via `ICodeHost` — never Octokit directly in agent
- Must have a corresponding unit test with mocked `ILanguageModel`
- Must be stateless — no instance fields that change between calls

---

## State Machine Rules

The state machine lives exclusively in `Zephyrus.Core/Pipeline/PipelineStateMachine.cs`.

**Valid transitions only:**
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

---

## GitHub Integration Rules

- **GitHub is source of truth for code** — DB tracks pipeline state only
- Never store file contents in the database
- Branch naming: `feature/{feature-slug}/{task-id}`
- PR title format: `[Zephyrus] {task-title} (#{issue-number})`
- All PRs must be linked to their originating GitHub Issue
- All Octokit.net calls live in `Zephyrus.Infrastructure/GitHub/` only
- The rest of the codebase uses `ICodeHost` — never Octokit types directly

---

## Testing Rules

- Every use case must have a unit test in `Zephyrus.Tests/Application/`
- Every agent must have a unit test in `Zephyrus.Tests/Infrastructure/AI/`
- Unit tests mock all infrastructure via Core interfaces
- No test may make a real HTTP call or DB call
- Integration tests (real DB, real GitHub) live in `Zephyrus.IntegrationTests/`
- Test naming: `{MethodName}_When{Condition}_Should{ExpectedResult}`

```
Zephyrus.Tests/
  Application/         ← Use case unit tests
  Core/                ← State machine, domain logic tests
  Infrastructure/
    AI/                ← Agent unit tests (mocked ILanguageModel)
    GitHub/            ← GitHub integration unit tests (mocked Octokit)

Zephyrus.IntegrationTests/
  Pipeline/            ← Full pipeline end-to-end tests
  GitHub/              ← Real GitHub API tests (requires token)
```

---

## Pre-Commit Checklist for Claude Code

Before finalising any implementation, verify:

- [ ] No `HttpClient` instantiation outside `Zephyrus.Infrastructure`
- [ ] No `DbContext` reference outside `Zephyrus.Infrastructure/Persistence/`
- [ ] No Octokit types referenced outside `Zephyrus.Infrastructure/GitHub/`
- [ ] No business logic in `Zephyrus.Api` controllers
- [ ] `Zephyrus.Core` has zero `<PackageReference>` entries
- [ ] `Zephyrus.Application` references only `Zephyrus.Core`
- [ ] Every new agent has a corresponding unit test
- [ ] Every new public method has XML doc comments
- [ ] All new entities follow ubiquitous language naming
- [ ] State machine not bypassed — all status changes go through `PipelineStateMachine`

---

## What To Do When Unsure Where Something Goes

Ask this sequence of questions:

1. **Is it a domain rule or entity?** → `Zephyrus.Core`
2. **Is it a use case or orchestration step?** → `Zephyrus.Application`
3. **Is it a call to an external system (DB, GitHub, Claude)?** → `Zephyrus.Infrastructure`
4. **Is it HTTP in/out or DI wiring?** → `Zephyrus.Api`
5. **Still unsure?** → Default to `Zephyrus.Core` and work outward

---

*This document must be updated whenever a significant architectural decision is made.
It takes precedence over CLAUDE.md in case of conflict on structural matters.*