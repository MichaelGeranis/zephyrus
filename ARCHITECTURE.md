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
- Application-level exceptions: `ResourceNotFoundException`, `RequestRejectedException`, `UnauthorizedApprovalException`

**What does NOT belong here:**
- EF Core, Octokit, HttpClient references
- Controller or HTTP concerns
- Direct instantiation of Infrastructure classes

**Dependency rule check:** Application .csproj references only `Zephyrus.Core`.

### Zephyrus.Infrastructure — Frameworks & Drivers Layer

**What belongs here:**
- EF Core: `ZephyrusDbContext`, migrations, repository implementations
- Octokit.net: `GitHubCodeHost` implementing `ICodeHost`
- Language models: `ClaudeLanguageModel` and `OpenAiCompatibleLanguageModel` (local models) implementing `ILanguageModel`, combined by `FallbackLanguageModel`
- Job queue: `DurableJobQueue` implementing `IJobQueue` over the `agent_jobs` table, plus the `AgentJobWorker` background service
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
  Jobs/               ← Job queue and the background worker that runs agent jobs
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
- `proxy.ts` sends a Content-Security-Policy with a fresh script nonce on every
  page (`lib/csp.ts`). The UI holds a team token in the browser, so the policy
  runs only Next's own nonce-carrying scripts, lets the browser connect only to
  the site and the API (`NEXT_PUBLIC_API_URL`), and loads images from the site
  only. Pages are rendered per request because a prebuilt page cannot carry a
  per-request nonce.
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

AgentInvocation (usage columns)
  model           string         ← model that answered, e.g. "llama3.1:8b → claude-opus-5-5"
  input_tokens    int
  output_tokens   int
  estimated_cost_usd decimal     ← null when the model has no price
  prompt_version  string         ← first 12 hex of SHA-256 of the system prompt
  artifact_id     UUID           ← artifact the run produced (no FK: kept after a re-run replaces it)

AgentJobRecord
  id              UUID, PK
  feature_id      UUID, FK → Feature (cascade delete)
  kind            enum           ← AgentJobKind: Prd | Architect | Task | Code | Qa | DevOps
  force_rerun     bool           ← a person asked to re-run the step
  status          enum           ← AgentJobStatus: Queued | Running | Succeeded | Failed
  attempts        int            ← at most AgentJobRecord.MaxAttempts (2)
  last_error      text
  enqueued_at     timestamp
  next_attempt_at timestamp      ← when a queued job is due
  lease_expires_at timestamp     ← while running; another worker may claim after it
  completed_at    timestamp
  concurrency_stamp UUID         ← changes on every state change
  trace_parent    string         ← W3C trace context of the request that queued it

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

### Reaching Deployed

`Deployed` is the one transition no approval can trigger. Approving the
generated workflow file reviews the CI/CD config; it does not mean anything
shipped. The feature moves to `Deployed` only when a real deployment succeeds:

```
PR merged (webhook)      ──> TaskItem → Done
                             Deployment(sha, environment) → Pending
deployment_status success ──> Deployment → Success
                             Feature: QaApproved → Deployed + PipelineEvent
```

- Deployments are matched to features by commit sha. The merge webhook is what
  establishes that mapping, using `TaskItem.PrId` to find the feature.
- A deployment that succeeds while the feature is not at `QaApproved` is
  recorded but does not advance the pipeline.
- A failed deployment marks the `Deployment` failed and leaves the feature at
  `QaApproved`, so a later attempt can still carry it forward.

### Secrets at rest

`Project.GitHubToken` is a code-host credential, so it is encrypted in the
database rather than stored as written.

- `ISecretProtector` (Core) is implemented by `DataProtectionSecretProtector`
  (Infrastructure) over ASP.NET Core Data Protection.
- It is applied as an EF Core value conversion on the `github_token` column, so
  entities always hold the plaintext and none of the ten call sites that pass
  the token to `ICodeHostFactory` had to change.
- Stored values carry an `enc:v1:` prefix. A value without it is a token written
  before encryption existed: it is read back as-is and re-written encrypted the
  next time its project is saved, so no data migration is needed. A prefixed
  value that fails to decrypt throws rather than returning ciphertext.
- **The key ring is operational state.** Set `DataProtection:KeyPath` to a
  durable, backed-up directory. The default location lives under the host user
  profile and is lost when a container is replaced — and losing the keys means
  every stored token must be re-entered.

### Demo mode

`Demo:Enabled` swaps the two external systems for local stand-ins in
`Zephyrus.Infrastructure/Demo/`, behind the same Core interfaces:

- `DemoLanguageModel` (`ILanguageModel`) returns canned output in the format
  each agent parses, recognising the agent from its system prompt.
- `FileSystemCodeHost` (`ICodeHost`) writes branches, issues and pull requests
  under `Demo:RepositoryRoot`, rejecting any path that would leave that folder.

On startup in demo mode, `SeedDemoDataUseCase` creates a sample project when
there are none. Nothing else changes: the state machine, approval authority,
job queue and audit trail run exactly as in production, which is the point of
the demo. Outside demo mode, the API refuses to start if any team token starts
with `demo-`, so the public Docker demo tokens cannot reach a real deployment.

The Docker setup (`docker-compose.yml`, `docker/`) runs with
`Database:MigrateOnStartup` so a fresh database is migrated automatically.

### GitHub App

`GitHubCodeHost` authenticates each call for the repository it names.
`GitHubAppTokenSource` (Infrastructure/GitHub) signs an RS256 JWT with the App
key (`GitHubAppJwt`), looks up the App's installation on that repository and
mints an installation token, cached until five minutes before it expires. A
repository without the App is remembered for five minutes and uses the
project's stored token, as does any failure to reach GitHub (logged). No
call site changed: `ICodeHostFactory.Create(token)` still takes the project
token, now as the fallback. The two GitHub App API calls sit behind
`IGitHubAppApi`, so tests make no HTTP calls.

The App's webhooks use the same endpoint and secret as repository webhooks.

### Webhook authenticity

`POST /api/webhooks/github` is a public endpoint, so the payload signature is
the only thing authenticating it. Deliveries are rejected unless
`X-Hub-Signature-256` matches an HMAC-SHA256 of the **raw** body under
`GitHub:Webhook:Secret`, compared with `FixedTimeEquals`. The body cannot be
re-serialised from a parsed model — the signature covers exactly the bytes
GitHub sent. An unset secret rejects everything.

---

## Authentication and Approval Authority

The state machine decides *when* a transition is legal. Approval authority
decides *who* may trigger it. Both are domain rules and both live in
`Zephyrus.Core/Pipeline/`.

```
ApprovalAuthority.cs      ← which roles may approve which artifact type
PipelineStateMachine.cs   ← which status transitions are valid
```

`TeamRole` mirrors the three roles in BUSINESS.md: `PmEm`, `TechLead`, `Qa`.

| Artifact | Roles allowed to approve |
|----------|--------------------------|
| Prd      | PmEm |
| Adr      | TechLead |
| Task     | PmEm or TechLead |
| Pr       | TechLead |
| Test     | Qa |
| Workflow | TechLead |

Changing which models a project's stages use is also limited to the Tech Lead
(`ModelChoiceAuthority`, 403 otherwise).

**Rules:**
- The approver is taken from the authenticated principal via `IUserContext`
  (`Zephyrus.Core/Interfaces/`) — never from the request body. This is what makes
  `Artifact.ApprovedBy` and `PipelineEvent.TriggeredBy` trustworthy as an audit trail.
- An unauthenticated caller, or one whose roles do not cover the artifact type,
  gets `UnauthorizedApprovalException` — mapped to 403 (401 when no credentials
  were presented at all).
- A Task artifact names two roles. An artifact is approved exactly once by one
  person, so either role satisfies the gate; requiring two distinct sign-offs
  would need a second approval record.

### Authentication scheme

`TeamToken` is a custom `AuthenticationHandler` in `Zephyrus.Api/Authentication/`.
It matches an `Authorization: Bearer <token>` header against the configured team
roster and issues a principal carrying the member's role claims.

- The roster lives in configuration (`Team:Members`), not the database —
  Zephyrus targets a small fixed team. Moving it to a `TeamMember` entity is a
  contained change behind the same `IUserContext`.
- Tokens are compared with `CryptographicOperations.FixedTimeEquals`.
- A member with an empty configured token can never authenticate, so the shipped
  defaults deny everyone until real tokens are supplied.
- **Authentication is required by default.** A fallback authorization policy
  rejects any anonymous request with 401, so a new endpoint is protected without
  remembering an attribute. The only `[AllowAnonymous]` endpoint is the GitHub
  webhook, which is authenticated by its payload signature instead (see
  *Webhook authenticity*). Role checks remain specific to approvals.
- Every API response carries `X-Content-Type-Options: nosniff`,
  `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` and
  `Cache-Control: no-store`; HSTS is enabled outside Development. Allowed CORS
  origins come from `Cors:AllowedOrigins`.

### Failed sign-ins

`FailedSignInLimiter` counts wrong team tokens per client IP address in fixed
windows (`Authentication:FailedSignIns`, default 10 in 15 minutes). Once an
address reaches the limit, `FailedSignInThrottleMiddleware` answers 429 with
`Retry-After` to every request from it that carries a bearer token, before the
token is checked, until the window ends. That includes valid tokens: checking
them would let the client keep guessing. Requests without a token, such as
GitHub webhooks, are not affected.

- Counts are in memory, per API instance, and reset on restart.
- Behind a reverse proxy every caller shares the proxy's address, so the limit
  needs `ReverseProxy:Enabled` (see *Running behind a reverse proxy*).

### Running behind a reverse proxy

`ReverseProxyOptions` (`Zephyrus.Api/Hosting/`) turns on ASP.NET Core forwarded
headers, first in the pipeline, so the sign-in limit sees the client's address
and HSTS sees the original scheme.

- Headers are trusted only from `ReverseProxy:KnownProxies` or
  `ReverseProxy:KnownNetworks`; the API refuses to start with the setting on
  and both empty. From any other address they are ignored, so a client cannot
  choose its own address to escape the limit.
- `ForwardLimit` is 1: only the entry the nearest proxy added is used.

### Health endpoints

`HealthEndpoints` (`Zephyrus.Api/Hosting/`) maps two anonymous endpoints that
report the status and version and nothing else:

- `/health`, liveness, runs no checks, so a database outage does not get the
  API restarted.
- `/health/ready`, readiness, also checks the database through
  `DatabaseHealthProbe` (`Infrastructure/Persistence/`, since only Persistence
  touches the DbContext), and answers 503 when it fails.

The web UI has its own `/health`. Both Docker images and `docker-compose.yml`
declare health checks on them; the web service starts once the API is ready.

### Error responses

`ExceptionHandlingMiddleware` returns problem details. A message reaches the
caller only from exceptions written for callers:

| Exception | Status |
|-----------|--------|
| `ResourceNotFoundException`, `ArtifactNotFoundException` | 404 |
| `RequestRejectedException` | 400 |
| `UnauthorizedApprovalException`, `NotPermittedException` | 403 |
| `InvalidTransitionException` | 409 |
| `GeneratedFileRejectedException` | 422 |
| anything else | 500, generic message and the trace ID |

.NET, EF Core, Octokit and the model clients throw `InvalidOperationException`
and `ArgumentException` with internal details, so those are never shown. Throw
`ResourceNotFoundException` or `RequestRejectedException` from Application
when the caller should see why.

---

## Orchestrator Design

The Orchestrator is a **deterministic state machine — not an AI**. It:

1. Listens for approval events from the UI
2. Listens for GitHub webhook events (PR merged, CI passed/failed)
3. Builds the correct context bundle for the next agent
4. Invokes the next agent asynchronously via the job queue
5. Persists state transitions to `PipelineEvent`

```csharp
// The orchestrator maps a status to a job and queues it. It never runs an agent.
public async Task OnArtifactApprovedAsync(Guid featureId, FeatureStatus newStatus, CancellationToken ct)
{
    if (!TriggerMap.TryGetValue(newStatus, out var kind))
        return;

    await _jobQueue.EnqueueAsync(new AgentJob(featureId, kind), ct);
}
```

**Key rule:** Agents are stateless. The Orchestrator is stateful. Never put state inside an agent.

### Background execution

Agent runs take minutes — the Code Agent runs one pass per task, plus up to
three follow-up passes each — so they must never execute inside the HTTP request
that triggered them.

```
Approve request ──> ApproveArtifactUseCase ──> PipelineOrchestrator
                              │                        │
                    advance + audit event         IJobQueue.EnqueueAsync
                              │                        │
                       response returns          DurableJobQueue
                                              (row in agent_jobs, Queued)
                                                       │
                                                 AgentJobWorker
                                     (claims a job, own DI scope per job)
                                                       │
                                              IAgentJobDispatcher ──> Invoke*AgentUseCase
```

Requests that ask for a run go the same way. `generate-prd` and `rerun-step`
check the request (the feature exists, its status allows the step), queue the
job through `AgentRunRequests` and return **202 Accepted**. A request is
rejected while another agent job of the same feature is waiting or running,
because two runs would race to write the same artifacts. A named re-run step
is queued with `ForceRerun`, so it runs even if the feature has moved on.

- `IJobQueue`, `IAgentJobDispatcher` and `IAgentJobRepository` live in
  `Zephyrus.Core`, so `Infrastructure` runs jobs without ever referencing
  `Application`.
- **Jobs are stored, not held in memory.** `DurableJobQueue` writes each job to
  the `agent_jobs` table, so queued work survives a restart. The worker is woken
  as soon as a job is queued and also polls (`AgentJobs:PollInterval`), which
  picks up retries and jobs queued by another instance.
- **The retry policy is a domain rule** on `AgentJobRecord` (Core): a failed job
  is retried once after `AgentJobs:RetryDelay`, then marked `Failed` with its
  error. The feature keeps its current status throughout, so a person recovers
  with the existing `rerun-step` endpoint, as BUSINESS.md requires.
- **Leases recover crashed work.** A claimed job holds a lease
  (`AgentJobs:Lease`, longer than the slowest agent run). If the process stops
  mid-job, the lease expires and the job is claimed again as its next attempt;
  a job interrupted on its last attempt is marked `Failed`.
- **Claims are safe across workers.** Claiming bumps a concurrency stamp, so if
  two workers race for one job the second save is rejected and that worker
  moves on. This uses EF Core optimistic concurrency rather than database row
  locks, so it behaves the same on PostgreSQL and on the SQLite test database.
- The worker creates a **new DI scope per job**. The request scope that enqueued
  the job is already disposed by the time the job runs.
- Job status is served at `GET /api/features/{id}/agent-jobs` and shown on the
  feature page, which refreshes while a job is waiting or running.

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
- Files it generates are checked against `GeneratedFilePolicy` before any is committed (see below)

### Generated file paths

The Code and QA Agents choose the paths of the files they write, and the model
behind them reads text an attacker can influence, such as issue bodies. Their
use cases call `GeneratedFilePolicy.EnsureAllowed` (`Zephyrus.Core/Pipeline/`)
before committing anything. It rejects:

- malformed paths: empty, absolute, backslashes, `.` or `..` segments;
- anything that runs code or grants access outside code review: `.github/`,
  other CI configuration, `.git` and `.gitmodules`, `CODEOWNERS`, commit hooks
  (`.husky/`), `.devcontainer/` and `.vscode/`;
- Zephyrus's own approved artifacts under `docs/prd/`, `docs/adr/`,
  `docs/tasks/` and `docs/qa/`.

A rejected run commits none of its files and fails with
`GeneratedFileRejectedException`, naming each path. The DevOps Agent's
workflow file is not subject to it: it has its own Tech Lead approval gate.

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

### Language models

Agents depend only on `ILanguageModel` (Core). Infrastructure provides:

- `ClaudeLanguageModel`: the Claude API. It opts into server-side refusal
  fallback and treats `stop_reason: "refusal"` as a failure.
- `OpenAiCompatibleLanguageModel`: any OpenAI-compatible server, which is how
  local models (Ollama, LM Studio, vLLM) are reached.
- `FallbackLanguageModel`: a primary and an optional fallback. It falls back
  when the primary throws, and goes straight to the fallback when
  `LanguageModelRouting.PreferFallback` is set. `AgentJobWorker` sets that on a
  job's retry, because the first attempt's failure is often malformed output
  that this class cannot see.

`LanguageModelChainFactory` builds chains from `LanguageModel:Default` and
`LanguageModel:Stages`. Each agent is registered with its stage's chain, or the
shared `ILanguageModel` when its stage has no override, so demo mode and test
fakes need no special handling.

**Per-project choice.** A project can choose its own chain per stage.
Choices are an append-only log, `ModelChoiceChange` (Core, table
`model_choice_changes`): a stage's current choice is its latest change, and a
reset returns it to the configuration. `ModelChoiceAuthority` lets only a Tech
Lead change them, since they decide cost and whether project content goes to
a hosted model; the changer comes from `IUserContext`, as for approvals.

Agents are built before a job says which project it is for, so each agent's
model is a `StageLanguageModel` that looks the choice up on every call.
`AgentJobDispatcher`, which every agent run passes through, loads the
feature's project choices into the scoped `IProjectModelSelection` before
running the agent. `IModelCatalog` validates references and lists the known
models.

**Usage accounting.** Each model call records its model and tokens in the
scoped `LanguageModelUsageTracker`. `AgentInvocationRepository.AddAsync`
drains the tracker into the run being saved: the model calls made since the
last save in the same scope belong to that run. Cost is estimated from
`ModelPricing` at that moment and stored, so a later price change does not
rewrite history. Agents and use cases stay unaware of this. `FeatureUsage`
(Core) sums a feature's runs for the API.

### Provenance and audit export

Every agent run records the **prompt version** (the first 12 hex characters of
the SHA-256 of its system prompt, which is the prompt file) and the **artifact
it produced**. The use cases create the artifact in memory first, so its ID is
known when the run is saved. The Code Agent's runs, one per task, all name the
feature's Pr artifact. There is no foreign key: a re-run deletes the artifact
it replaces, and the run that produced the old one stays in the record.

`AuditExportManager` (Application) assembles the audit trail as one document,
format `zephyrus.audit/v1`, served as a JSON download:

- `GET /api/features/{id}/audit-export`
- `GET /api/projects/{id}/audit-export` (every feature, plus the project's
  model choice changes)

It contains the pipeline events with who triggered each, the artifacts with
approver and the runs that produced them, every agent run with prompts,
response, model, prompt version, tokens and cost, the agent jobs, tasks and
deployments. Artifact contents stay in the repository, and the project's
code-host token is never included. Any team member may export.

### Tracing

OpenTelemetry is wired in `Program.cs` and is on only when
`OTEL_EXPORTER_OTLP_ENDPOINT` is set. It records:

- incoming requests (ASP.NET Core), outgoing HTTP, and database queries
  (Npgsql's own trace source);
- spans Zephyrus creates itself on the `Zephyrus` trace source
  (`Infrastructure/Telemetry/ZephyrusTelemetry.cs`):
  - `agent job {kind}`, one per job attempt;
  - `chat {model}` for every model call, tagged with `gen_ai.*` attributes.

`DurableJobQueue` stores the W3C `traceparent` of the request that queued a
job (`agent_jobs.trace_parent`). `AgentJobWorker` starts the job's span as a
child of it, so the background work joins the request's trace. Core only
stores the string; it does not reference any telemetry library.

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