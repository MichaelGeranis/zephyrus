# Zephyrus

> Like the west wind that nourishes the Elysian Fields, Zephyrus breathes life into ideas --
> turning prompts into PRDs, architecture, code, tests, and deployed software through a
> pipeline of specialized AI agents with human validation at every gate.

Zephyrus is an AI-powered software delivery platform that orchestrates the full journey
from idea to production. It is designed for a **3-person amplified team** that produces
the output of 10, by placing AI agents at every execution step and humans at every
validation gate.

```
Idea (prompt) --> PRD --> Architecture --> Tasks --> Code --> QA --> Deploy
```

> **Deep dives:** [ARCHITECTURE.md](ARCHITECTURE.md) for technical architecture | [BUSINESS.md](BUSINESS.md) for business rules and domain logic

---

## Tech Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Backend / Orchestrator | ASP.NET Core (C#) | .NET 8.0 |
| Frontend | Next.js (TypeScript) | 15.x |
| Database | PostgreSQL + Entity Framework Core | EF Core 8.0 |
| AI Layer | Claude API via HttpClient wrapper | claude-sonnet-4-20250514 |
| GitHub Integration | Octokit.net | 13.x |
| CSS | Tailwind CSS | 4.x |
| Testing | xUnit + SQLite in-memory | xUnit 2.9 |

---

## Architecture

Clean Architecture (Robert C. Martin) with four layers:

```
+-----------------------------------------+
|         Frameworks & Drivers            |  <-- Web, DB, GitHub, Claude API
|  +-----------------------------------+  |
|  |       Interface Adapters          |  |  <-- Controllers, DTOs
|  |  +-----------------------------+  |  |
|  |  |        Use Cases            |  |  |  <-- Application business rules
|  |  |  +-----------------------+  |  |  |
|  |  |  |      Entities         |  |  |  |  <-- Core domain, state machine
|  |  |  +-----------------------+  |  |  |
|  |  +-----------------------------+  |  |
|  +-----------------------------------+  |
+-----------------------------------------+
```

**Dependency rule**: Dependencies always point inward. Inner layers know nothing about outer layers.

### Project Structure

```
src/
  Zephyrus.Core/                 # Domain entities, interfaces, enums, state machine
  Zephyrus.Application/          # Use cases, orchestrator, managers
  Zephyrus.Infrastructure/       # EF Core, GitHub (Octokit), Claude API, agents
  Zephyrus.Api/                  # ASP.NET Core controllers (thin)
  Zephyrus.Web/                  # Next.js frontend (thin presentation layer)

tests/
  Zephyrus.IntegrationTests/    # xUnit integration tests with SQLite + fakes

docs/                            # Agent-generated PRDs and ADRs (committed by agents)
infra/                           # Example Project Constitution
```

### Dependency Map

```
Zephyrus.Api
  +-- depends on --> Zephyrus.Application
                       +-- depends on --> Zephyrus.Core
                                            +-- depends on --> nothing

Zephyrus.Infrastructure
  +-- depends on --> Zephyrus.Core (implements its interfaces)
  +-- registered in --> Zephyrus.Api (DI container)

Zephyrus.Web
  +-- talks to --> Zephyrus.Api (over REST)
```

---

## API Reference

### Projects

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/projects` | Create a new project |
| GET | `/api/projects` | List all projects |
| GET | `/api/projects/{id}` | Get project by ID |

### Features

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/features` | Create a new feature (idea prompt) |
| GET | `/api/features/{id}` | Get feature by ID |
| GET | `/api/features/by-project/{projectId}` | List features for a project |
| POST | `/api/features/{id}/generate-prd` | Invoke the PRD Agent |

### Artifacts

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/features/{id}/artifacts` | List artifacts for a feature |
| GET | `/api/features/{id}/artifacts/{artifactId}/content` | Get artifact content from GitHub |
| POST | `/api/features/{id}/artifacts/{artifactId}/approve` | Approve an artifact (advances pipeline) |

---

## Data Model

### Entities

| Entity | Purpose |
|--------|---------|
| **Project** | A software project with its configuration (Project Constitution) |
| **Feature** | A unit of work moving through the delivery pipeline |
| **Artifact** | An output produced by an agent (PRD, ADR, PR, tests) |
| **TaskItem** | A granular work item linked to a GitHub Issue |
| **PipelineEvent** | Audit log of all state transitions |
| **Deployment** | Record of a deployment to an environment |

### Feature State Machine

```
Ideation --> PrdPending --> PrdApproved --> ArchPending --> ArchApproved
  --> TasksPending --> TasksApproved --> Coding --> QaPending --> QaApproved --> Deployed
```

Every transition is enforced by `PipelineStateMachine`. No invalid transitions are possible.

---

## AI Agents

Each agent is stateless and follows the `IAgent<TInput, TOutput>` contract:

| Agent | Input | Output | Artifact |
|-------|-------|--------|----------|
| PRD Agent | Feature prompt + Constitution | Markdown PRD | `docs/prd-{slug}.md` |
| Architect Agent | Approved PRD + Constitution | Markdown ADR | `docs/adr-{slug}.md` |
| Task Agent | PRD + ADR + Constitution | GitHub Issues | Issues on repo |
| Code Agent | Issue + ADR + Constitution + code | Implementation | Feature branch + PR |
| QA Agent | PR diff + Issue + criteria | Tests + report | Test files on PR branch |
| DevOps Agent | Constitution + deploy target | CI/CD workflow | `.github/workflows/` |

The **PipelineOrchestrator** is a deterministic state machine (not AI) that wires approval events to next-agent invocations.

---

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [PostgreSQL 15+](https://www.postgresql.org/) (or Docker)

### 1. Clone the repository

```bash
git clone https://github.com/your-org/zephyrus.git
cd zephyrus
```

### 2. Configure the backend

Copy and edit the app settings:

```bash
cd src/Zephyrus.Api
```

Edit `appsettings.Development.json` with your credentials:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=zephyrus;Username=postgres;Password=postgres"
  },
  "GitHub": {
    "Token": "ghp_your_github_token"
  },
  "Claude": {
    "ApiKey": "sk-ant-your_claude_api_key",
    "Model": "claude-sonnet-4-20250514",
    "MaxTokens": 4096
  }
}
```

### 3. Apply database migrations

```bash
cd src/Zephyrus.Api
dotnet ef database update --project ../Zephyrus.Infrastructure
```

### 4. Run the backend

```bash
cd src/Zephyrus.Api
dotnet run
```

The API starts at `http://localhost:5000`.

### 5. Run the frontend

```bash
cd src/Zephyrus.Web
npm install
npm run dev
```

The UI starts at `http://localhost:3000`.

### 6. Run the tests

```bash
dotnet test
```

This runs all integration tests using SQLite in-memory and fake external services (no GitHub token or Claude API key required).

---

## Testing Strategy

Integration tests live in `tests/Zephyrus.IntegrationTests/` and cover:

- **Use-case level**: Full pipeline flow with real repositories backed by SQLite
- **HTTP API level**: Every endpoint tested via `WebApplicationFactory` with the full ASP.NET Core stack

External services are replaced with fakes:

| Real Service | Test Fake | Behavior |
|-------------|-----------|----------|
| PostgreSQL | SQLite in-memory | Shared connection, schema auto-created |
| GitHub (Octokit) | `FakeCodeHost` | Dictionary-backed file storage |
| Claude API | `FakeLanguageModel` | Returns canned markdown per agent type |

---

## Contributing

### Development workflow

1. **Fork** and clone the repository.
2. Create a feature branch: `git checkout -b feature/your-feature`.
3. Make changes following the architecture rules below.
4. Run tests: `dotnet test`.
5. Commit using [conventional commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `refactor:`, etc.
6. Open a pull request against `main`.

### Architecture rules

- **Dependency rule is absolute**: Core depends on nothing. Application depends only on Core. Infrastructure implements Core interfaces. Api is a thin adapter.
- **No business logic in the frontend**: `Zephyrus.Web` is a presentation-only layer. All validation, state transitions, and orchestration happen server-side.
- **No business logic in controllers**: Controllers map HTTP to use cases and back. Nothing more.
- **Agents are stateless**: All state lives in the database and GitHub. Agents take input, produce output, and have no side effects beyond the orchestrator's control.
- **GitHub is source of truth for code artifacts**: Never duplicate file contents in the database. Store only the repository path.
- **State machine governs all transitions**: Use `Feature.Advance()` for pipeline progression. Never set `Feature.Status` directly outside the domain entity.

### Code conventions

- **Backend**: C# with nullable reference types enabled. `dotnet format` for style.
- **Frontend**: TypeScript strict mode. Tailwind CSS for styling.
- **Testing**: xUnit for backend. Integration tests with fakes over unit tests with mocks.
- **Commits**: Conventional commits. Squash on merge.
- **Branches**: `feature/{description}` off `main`.

### Adding a new agent

1. Define `{Agent}Input` and `{Agent}Output` in `Zephyrus.Core/Agents/`.
2. Create `{Agent}Agent : IAgent<TInput, TOutput>` in `Zephyrus.Infrastructure/AI/Agents/`.
3. Create `Invoke{Agent}AgentUseCase` in `Zephyrus.Application/UseCases/`.
4. Wire into `PipelineOrchestrator` for the appropriate approval trigger.
5. Register in `DependencyInjection.cs` (both Application and Infrastructure).
6. Add integration tests.

### Environment variables

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `GitHub__Token` | GitHub personal access token |
| `Claude__ApiKey` | Anthropic API key |
| `Claude__Model` | Claude model ID (default: `claude-sonnet-4-20250514`) |
| `Claude__MaxTokens` | Max output tokens (default: `4096`) |
| `NEXT_PUBLIC_API_URL` | Backend API URL for the frontend (default: `http://localhost:5000`) |

---

## License

TBD
