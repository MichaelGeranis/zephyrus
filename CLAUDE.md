# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Zephyrus is an AI-powered software delivery platform that orchestrates the journey from idea to production. AI agents execute each step (PRD, architecture, tasks, code, QA, deploy) while humans validate at every gate. Designed for a 3-person amplified team. GitHub is the delivery layer.

**Extra Context**

- Always consult [ARCHITECTURE.md](ARCHITECTURE.md) alongside this file when making architectural decisions.
- Always update [ARCHITECTURE.md](ARCHITECTURE.md) when making architectural changes to the project.
- Always consult [BUSINESS.md](BUSINESS.md) for domain rules, pipeline details, and business logic when making relevant decisions.
- Always update [BUSINESS.md](BUSINESS.md) when business logic changes.
- Always consult [README.md](README.md) for tech stack, project structure, and contribution rules when contributing to this project.
- Always update [README.md](README.md) when for relevant changes.


---

## Common Commands

### Backend (.NET)
```bash
dotnet build Zephyrus.sln                    # Build entire solution
dotnet run --project src/Zephyrus.Api        # Run API (http://localhost:5000)
dotnet format                                 # Format backend code
```

### Frontend (Next.js)
```bash
cd src/Zephyrus.Web && npm install           # Install dependencies
cd src/Zephyrus.Web && npm run dev           # Dev server (http://localhost:3000)
cd src/Zephyrus.Web && npm run lint          # Lint frontend
```

### Testing
```bash
dotnet test                                   # Run all tests
dotnet test tests/Zephyrus.IntegrationTests   # Run integration tests only
dotnet test --filter "TestMethodName"          # Run a single test by name
dotnet test --filter "ClassName"               # Run all tests in a class
```

### Database Migrations
```bash
dotnet ef database update --project src/Zephyrus.Infrastructure --startup-project src/Zephyrus.Api
dotnet ef migrations add MigrationName --project src/Zephyrus.Infrastructure --startup-project src/Zephyrus.Api
```

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Next.js (TypeScript) + Tailwind CSS |
| Backend / Orchestrator | .NET 8.0 (C#) |
| Database | PostgreSQL + Entity Framework Core 8.0 |
| Background Jobs | Hangfire (or IHostedService for MVP) |
| AI Layer | Claude API via HttpClient wrapper |
| GitHub Integration | Octokit.net |
| Testing | xUnit + SQLite in-memory (no external services needed) |

**No RAG for MVP.** Full repo is passed in context.

---

## Architecture

```
src/
  Zephyrus.Core/              ← Entities, interfaces, enums, state machine (zero dependencies)
  Zephyrus.Application/       ← Use cases, orchestrator (depends only on Core)
  Zephyrus.Infrastructure/    ← EF Core, Octokit, Claude API, agents (implements Core interfaces)
  Zephyrus.Api/               ← ASP.NET Core controllers, DI wiring (thin adapter)
  Zephyrus.Web/               ← Next.js frontend (thin presentation layer)

tests/
  Zephyrus.IntegrationTests/  ← xUnit with SQLite + fakes
```

### Dependency Rule
```
Api → Application → Core ← Infrastructure
```
Core depends on nothing. Application depends only on Core. Infrastructure implements Core interfaces. Api is a thin adapter. Web talks to Api over REST.

---

## Coding Constraints

- **Dependency rule is absolute**: Never reference outer layers from inner layers.
- **No business logic in controllers**: Controllers map HTTP to use cases and back.
- **No business logic in frontend**: Zephyrus.Web renders API responses — nothing more.
- **Agents are stateless**: All state lives in DB and GitHub. Agents take input, produce output.
- **GitHub is source of truth for code**: DB stores paths, never file contents.
- **State machine governs transitions**: Use `Feature.Advance()` — never set status directly.
- **Core has zero NuGet packages**: Enforce this strictly.

---

## Build Order

Implement in this exact order. Each step unblocks the next.

1. **DB schema + EF Core migrations**
2. **GitHub integration layer** — create branch, commit file, open PR, create issue
3. **PRD Agent** — first agent end-to-end
4. **Approval gate API** — `POST /features/{id}/artifacts/{artifactId}/approve`
5. **Approval gate UI** — Next.js: show artifact, edit inline, approve
6. **Orchestrator state machine** — wire approval → trigger next agent
7. **Architect Agent**
8. **Task Agent** — output is GitHub Issues
9. **Code Agent** — reads existing code, outputs PR
10. **QA Agent** — reads PR diff, outputs tests + CI report
11. **DevOps Agent** — generates GitHub Actions workflow
12. **Pipeline dashboard UI**

---

## What NOT to Build in MVP

- RAG / vector search
- Multi-repo support
- Multi-cloud / multi-environment deployment
- Infinite agent retry loops (one retry then human escalation)
- Multi-project portfolio view
- Mobile stack support
- Real-time WebSocket updates
- Role-based access control

---

## Session Start Convention

> "Read CLAUDE.md. We are building Zephyrus. Current build step: [STEP NUMBER AND NAME].
> Relevant existing code is in [PATH]. Proceed."
