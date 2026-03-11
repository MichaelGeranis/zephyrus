# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Zephyrus is an AI-powered software delivery platform that orchestrates the journey from idea to production. AI agents execute each step (PRD, architecture, tasks, code, QA, deploy) while humans validate at every gate. Designed for a 3-person amplified team. GitHub is the delivery layer.

# Bash commands 
### Backend (.NET)
```bash
dotnet build Zephyrus.sln                    # Build entire solution
dotnet run --project src/Zephyrus.Api        # Run API (http://localhost:5000)
dotnet format                                 # Format backend code
```

### Frontend (Next.js)
```bash
cd src/Zephyrus.Web; npm install           # Install dependencies
cd src/Zephyrus.Web; npm run dev           # Dev server (http://localhost:3000)
cd src/Zephyrus.Web; npm run lint          # Lint frontend
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

# Code style
- **Dependency rule is absolute**: Never reference outer layers from inner layers.
- **No business logic in controllers**: Controllers map HTTP to use cases and back.
- **No business logic in frontend**: Zephyrus.Web renders API responses — nothing more.
- **Agents are stateless**: All state lives in DB and GitHub. Agents take input, produce output.
- **GitHub is source of truth for code**: DB stores paths, never file contents.
- **State machine governs transitions**: Use `Feature.Advance()` — never set status directly.
- **Core has zero NuGet packages**: Enforce this strictly.

## Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| Use cases | `{Verb}{Noun}UseCase` | `ApproveArtifactUseCase` |
| Managers | `{Noun}Manager` | `FeatureManager` |
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


## SQL
- Keywords: UPPERCASE; identifiers lowercase
- Layout: One clause per line, 2-space indent for nesting

# Workflow rules
- Run the whole test suite after finishing a task to ensure you did not break anything.
- Be sure to change tests behavior only if the task requirements and acceptance.criteria suggests. Otherwise the code you wrote breaks another functionality and you should stop and not commit anything to git. 
- Format the files you edited.

## Branch Naming
- Feature: feat/short-description (e.g., feat/user-auth)
- Bug fix: fix/issue-number (e.g., fix/timeout-race)
- Documentation: docs/topic
- Never commit directly to main.
- Types: feat, fix, docs, refactor, test, chore

## Pull Requests
- Squash commits before merge (keep main linear)
- Resolve all review conversations before merging
- No merge conflicts—rebase on main if conflicts exist

## Testing
- 80% code coverage required
- Unit tests for all business logic
- Integration tests for API endpoints
- Unit tests mock all infrastructure via Core interfaces
- No test may make a real HTTP call or DB call
- Test naming: `{MethodName}_When{Condition}_Should{ExpectedResult}`

## Pre-Commit Checklist
- Before finalizing any implementation, verify:
- No `HttpClient` instantiation outside `Zephyrus.Infrastructure`
- No `DbContext` reference outside `Zephyrus.Infrastructure/Persistence/`
- No Octokit types referenced outside `Zephyrus.Infrastructure/GitHub/`
- No business logic in `Zephyrus.Api` controllers
- `Zephyrus.Core` has zero `<PackageReference>` entries
- `Zephyrus.Application` references only `Zephyrus.Core`
- Every new agent has a corresponding unit test
- All new entities follow ubiquitous language naming
- State machine not bypassed — all status changes go through `PipelineStateMachine`

## What To Do When Unsure Where Something Goes
1. **Is it a domain rule or entity?** → `Zephyrus.Core`
2. **Is it a use case or orchestration step?** → `Zephyrus.Application`
3. **Is it a call to an external system (DB, GitHub, Claude)?** → `Zephyrus.Infrastructure`
4. **Is it HTTP in/out or DI wiring?** → `Zephyrus.Api`
5. **Still unsure?** → Ask human


# Additional Instructions
- See @README.md for project overview, structure, tech stack.
This document is the project overview.
It takes precedence over CLAUDE.md in case of conflict on structural matters.
Update it whenever a significant decision is made that may affect its contents.
Do not add contents in this file unless instructed.
- See @ARCHITECTURE.md before making architectural changes to the project.
This document is the complete technical architecture reference.
It takes precedence over CLAUDE.md in case of conflict on structural matters.
Update it whenever a significant architectural decision is made.
- See @BUSINESS.md for application business rules andproduct  use cases.
This document is the product idea that resulted in this project.
It takes precedence over CLAUDE.md in case of conflict on business related matters.
Do not updated or add content in this file unless instructed by the human.

---

## Use Cases - Build Order - TBD after implementation

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