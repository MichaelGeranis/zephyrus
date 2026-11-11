# Changelog

All notable changes to Zephyrus are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project follows [Semantic Versioning](https://semver.org/). While the
version is 0.x, a minor release may include breaking changes; they are listed
under **Changed**.

## [Unreleased]

### Added

- [Production guide](docs/production.md): TLS through a reverse proxy, health
  checks, backups of the database and the key ring, and upgrades.
- Health endpoints: `/health` (liveness) and `/health/ready` (the database is
  reachable) on the API, `/health` on the web UI. They need no token and report
  the version. Both Docker images and the Compose file declare health checks.
- `ReverseProxy:Enabled` reads the client address from `X-Forwarded-For` and
  the scheme from `X-Forwarded-Proto`, from listed proxies only, so the failed
  sign-in limit counts each user separately behind a proxy.
- The web UI sends a Content-Security-Policy with a fresh script nonce on every
  page. It allows connections to the API only, and images from the site only.
- CI fails below 80% line coverage, for the backend and for the web UI's
  components and client code.

### Changed

- A GitHub webhook delivery that is not valid JSON is answered with 400 instead
  of 500.
- The web UI renders every page per request (needed for the CSP nonce).
- **.NET 11** (from .NET 10): every project targets `net11.0`, and the Docker
  images and CI use the .NET 11 SDK and runtime. ASP.NET Core and
  Microsoft.Extensions packages 11.0.0. Building from source needs the .NET 11
  SDK.
- EF Core 10.0.15 (from 10.0.14). EF Core 11 waits for a stable Npgsql 11
  provider; the current one, 10.0.3, requires EF Core 10. No database changes.
- OpenTelemetry 1.21.0, Swashbuckle.AspNetCore 11.0.0, Microsoft.NET.Test.Sdk
  18.12.0.
- Next.js and eslint-config-next 16.4.6, Testing Library user-event 14.6.9.
  TypeScript stays on 5.9: typescript-eslint, used by eslint-config-next, does
  not support TypeScript 7 yet.
- Ollama image 0.40.16; claude-code-action v1.0.271.
- Dependabot sends one grouped PR per ecosystem each week, and skips EF Core
  11, TypeScript 7 and new PostgreSQL majors until they can be adopted.

## [0.1.0] - 2026-11-10

The first release. Zephyrus carries a feature from idea to deployment through
role-checked approval gates, with every step recorded and exportable.

- **Run it:** `docker compose up` starts a demo with the released images; no
  API keys needed.
- **Governance:** approvals checked against the approver's role, models
  chosen per project by the Tech Lead, every artifact traced to the agent run,
  model and prompt version that produced it, and a JSON audit export.
- **Models:** local models (Ollama, LM Studio, vLLM) with a Claude fallback,
  per stage and per project, with tokens and estimated cost on every run.
- **Operations:** a durable agent job queue, OpenTelemetry tracing, and
  GitHub App support.

### Added

- Released images: `ghcr.io/michaelgeranis/zephyrus-api` and
  `zephyrus-web`, published by the release workflow for each version tag.
  `docker compose up` pulls them; `docker-compose.build.yml` builds from a
  checkout instead.

- GitHub App support: with `GitHub:App:AppId` and a private key configured,
  repositories the App is installed on are accessed with short-lived
  installation tokens; other repositories keep using the project's stored
  token, which is now optional when creating a project.
- Provenance: every agent run records its prompt version (a hash of the
  system prompt) and the artifact it produced, and the run page shows the
  model and prompt version. Existing runs get their prompt version when the
  database is migrated.
- Audit export: "Export audit trail" on the feature and project pages downloads
  the pipeline events, approvals, agent runs (with prompts, models, prompt
  versions and costs), jobs, tasks, deployments and model choice changes as
  JSON (`zephyrus.audit/v1`).
- Models per project: a Tech Lead can choose the primary and fallback model
  for each pipeline stage of a project, on the project page. Every change is
  recorded with who made it; other roles see the choices but cannot change
  them. `GET/PUT/DELETE /api/projects/{id}/models/{stage}`.
- Web UI tests. Component tests (Vitest and Testing Library) cover sign-in,
  the approval panel, the delete dialog, agent jobs and the API client.
  Playwright end-to-end tests run in CI against the Docker demo: sign-in, a
  QA member refused at the PRD gate, a PM/EM approving it and the approver
  recorded, and every page loading without errors.

- OpenTelemetry tracing, with Jaeger in Docker Compose at
  <http://localhost:16686>. An approval, the agent job it queues, and that
  job's model calls and database queries appear as one trace. Model calls
  carry `gen_ai.*` attributes (model, tokens).
- JSON console logs in Docker, carrying trace and span IDs.

- Local models through any OpenAI-compatible server (Ollama, LM Studio, vLLM),
  with a Claude fallback. The default is `llama3.1:8b`, falling back to Claude
  Opus 5.5; a failed agent job's retry runs on the fallback.
- A model chain per pipeline stage (`LanguageModel:Stages`).
- Every agent run records the model that answered, its tokens and an estimated
  cost; the feature page shows them, with a total per feature.
- Optional Ollama service in Docker Compose (`--profile ollama`).

- Agent jobs are stored in the database instead of in memory. They survive a
  restart, a failed job is retried once and then marked failed with its error,
  and a job interrupted by a crash is picked up again when its lease expires.
- The feature page lists its agent jobs with their status, attempts and
  errors, and refreshes while a job is waiting or running.

- `docker compose up` runs the database, API and web UI in one step.
- Demo mode: canned agent output and a local folder in place of GitHub, with
  a sample project, so Zephyrus can be tried without any API keys.
- Optional database migration on startup (`Database:MigrateOnStartup`).
- README demo GIF and screenshots.

- Apache-2.0 licence, `NOTICE`, contributing guide with Developer Certificate
  of Origin sign-off, security policy, code of conduct, issue and pull request
  templates, and this changelog.
- CI check that every commit in a pull request is signed off.

### Security

- An address that sends 10 wrong team tokens in 15 minutes is refused (429)
  for the rest of that window (`Authentication:FailedSignIns`).
- Unexpected errors no longer reach API callers: they get a generic message
  and a trace ID to quote, while the details go to the logs. Messages written
  for callers (not found, rejected requests) are shown as before, with the same
  status codes. The web UI shows the message instead of the raw response.
- The Code and QA Agents can no longer write to CI configuration, Git
  internals, editor and container settings, or approved artifacts, nor to
  paths outside the repository. A run that tries commits nothing and fails
  with the rejected paths.

- Next.js upgraded from 15 to 16.4.3, clearing the remaining high-severity
  npm advisories (Next.js and its bundled PostCSS). CI now fails on high npm
  advisories as well as critical ones.

- Fixed known high-severity advisories in dependencies:
  `System.Security.Cryptography.Xml` and `Pkcs` (through ASP.NET Core Data
  Protection), `Microsoft.OpenApi` (through Swashbuckle), and the test-only
  `SQLitePCLRaw`.
- CI fails when a NuGet package has a known high or critical advisory, or an
  npm package a critical one.
- GitHub Actions are pinned to commit SHAs, and Dependabot proposes weekly
  updates for NuGet, npm, GitHub Actions and Docker images.

### Changed

- `POST /api/features/{id}/generate-prd` and `rerun-step` queue the agent and
  return 202 Accepted with the feature, instead of running it inside the
  request. A request is refused while another agent job of the feature is
  waiting or running. The feature page disables the run buttons meanwhile and
  follows the job.
- **.NET 10** (from .NET 8): ASP.NET Core, EF Core and Microsoft.Extensions
  10.0.14, Npgsql 10.0.3, Octokit 14, OpenTelemetry 1.20.1. The Docker images
  use the .NET 10 SDK and runtime.
- **xUnit v3** (4.0.3, from 2.9) on Microsoft.Testing.Platform, selected for
  `dotnet test` in `global.json`. `dotnet test`, a project path and
  `--filter` work as before.
- **PostgreSQL 18** (from 16) in Docker Compose. An existing local database
  from an older version cannot be opened by Postgres 18: reset it with
  `docker compose down -v`.
- **Node.js 26** (from 20) for the web UI's Docker image and CI, with
  **ESLint 10**, react-markdown 10 and the Node 26 type definitions.

- The default Claude model is now Claude Opus 5.5. The previous default,
  Claude Sonnet 4, was retired in June 2026. Claude requests now set effort,
  allow 16,000 output tokens (thinking included), opt into server-side refusal
  fallback, and treat a refusal as a failure.

- Agent prompts ship with the API build instead of being located by walking up
  from the build folder, so the API runs the same from a publish folder or a
  container. `Prompts:Directory` overrides the location.
- Signed-out visitors see a sign-in prompt instead of empty pages.
- Markdown artifacts render with headings and lists (the typography plugin
  was missing).
- The API refuses to start with `demo-` team tokens unless demo mode is on.

- README rewritten with a project overview, status, badges and links to the
  project's policies.

### Fixed

- Tables, task lists and strikethrough in agent output now render on the
  review page (GitHub-flavoured Markdown), instead of showing as raw text.

- The pull request artifact no longer shows "Commit failed" after its pull
  requests were opened.

### Also in this release

Work from before this changelog was started, grouped by area.

#### Pipeline

- Feature pipeline from idea to deployment, governed by a forward-only state
  machine with an audit event for every transition.
- PRD, Architect, Task, Code, QA and DevOps agents; the Code Agent runs
  multiple passes with repository context.
- Agent runs execute on a background job queue rather than inside the request.
- Re-run any pipeline step by name, retry failed artifact commits, and edit
  artifact content before approval.
- A feature reaches `Deployed` only when a real deployment succeeds, driven by
  GitHub `pull_request` and `deployment_status` webhooks.

#### Governance and security

- Role-based approval gates (PM/EM, Tech Lead, QA), with the approver taken
  from the authenticated caller.
- Authentication required on every API endpoint; GitHub webhooks verified by
  HMAC signature.
- Per-project GitHub tokens encrypted at rest.
- Every agent invocation's prompt and response is recorded and viewable.
- Security response headers, configurable CORS origins, HSTS outside
  development.

#### Web UI

- Projects, features, artifact review and approval, pipeline dashboard, agent
  invocation viewer, deletion with confirmation.

#### Tooling

- CI for backend build and tests, and frontend lint, typecheck and build.
- Unit and integration test suites running without external services.

[Unreleased]: https://github.com/MichaelGeranis/zephyrus/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/MichaelGeranis/zephyrus/releases/tag/v0.1.0
