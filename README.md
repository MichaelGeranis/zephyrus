# Zephyrus

[![CI](https://github.com/MichaelGeranis/zephyrus/actions/workflows/ci.yml/badge.svg)](https://github.com/MichaelGeranis/zephyrus/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/MichaelGeranis/zephyrus)](https://github.com/MichaelGeranis/zephyrus/releases)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
![Status: alpha](https://img.shields.io/badge/status-alpha-orange.svg)

**An AI software delivery pipeline with a human approval gate at every step.**

Zephyrus takes a feature idea and carries it through PRD, architecture, task
breakdown, code, QA and deployment. AI agents do the work at each stage; the
right person on the team approves it before anything moves forward. Every
transition, approval and agent run is recorded, so you can always answer *who
approved this, and what produced it?*

```
Idea ──► PRD ──► Architecture ──► Tasks ──► Code ──► QA ──► Deploy
          ▲            ▲             ▲         ▲        ▲
        PM/EM      Tech Lead    PM/EM or TL  Tech Lead  QA      ← approval gates
```

Like the west wind that nourishes the Elysian Fields, Zephyrus breathes life
into ideas.

> **Status: alpha.** Zephyrus works end to end but is under active development
> and not yet hardened for production use.

![A QA member tries to approve the PRD and is refused: it needs the PM/EM role. The PM/EM approves it, the Architect Agent's job runs, and the timeline records who approved.](docs/images/demo.gif)

## Why Zephyrus

AI can write a PRD, an ADR or a pull request in minutes. What a small team
cannot afford is to ship that output unchecked. Zephyrus is built around
**governance**:

- **Role-based approval gates.** Only a PM/EM can approve a PRD, only the Tech
  Lead can approve architecture and code, only QA can approve test results.
  The approver is the authenticated user, never a name typed into a form.
- **A deterministic pipeline.** A forward-only state machine, not an AI,
  decides what happens next. Nothing advances without an approval.
- **A complete audit trail.** Every status change is a pipeline event with who
  triggered it. Every agent run keeps its full prompt and response. Each artifact
  is traced to the agent run, model and prompt version that produced it, and the
  whole trail exports as one JSON file per feature or project.
- **Deployment means deployed.** A feature reaches `Deployed` only when a real
  deployment succeeds, reported by GitHub — not when someone clicks approve.
- **GitHub is the source of truth.** PRDs, ADRs, tasks (as issues) and code
  (as pull requests) live in your repository, reviewable with your usual tools.

It is designed for small teams — a PM/EM, a Tech Lead and a QA engineer — who
want the output of a much larger team without giving up control.

## How it works

1. A **project** points at a GitHub repository and carries a
   [Project Constitution](CONSTITUTION.example.yaml): the stack, conventions and
   architecture rules every agent reads before acting.
2. A **feature** starts as a plain-language idea.
3. At each stage an **agent** produces an **artifact** and commits it to the
   repository. The pipeline stops at the approval gate.
4. When the right role approves, the **orchestrator** queues the next agent.
5. Merged pull requests and successful deployments arrive by webhook and close
   out the pipeline.

Business rules are in [BUSINESS.md](BUSINESS.md); the technical design is in
[ARCHITECTURE.md](ARCHITECTURE.md).

## Tech stack

| Layer | Technology |
|-------|-----------|
| Backend / orchestrator | ASP.NET Core 11 (C#), Clean Architecture |
| Frontend | Next.js 16 (TypeScript), Tailwind CSS 4 |
| Database | PostgreSQL 18, Entity Framework Core 10 |
| AI | Local models (Ollama, LM Studio, vLLM) and the Claude API, with fallback |
| Code host | GitHub (Octokit.net), through a GitHub App or per-project tokens |
| Testing | xUnit v3 on Microsoft.Testing.Platform, SQLite in-memory; Vitest and Testing Library (components); Playwright (end to end) |

## Run with Docker

The quickest way to try Zephyrus. You need [Docker](https://docs.docker.com/get-docker/)
and nothing else: no .NET, Node.js, database or API keys.

```bash
git clone https://github.com/MichaelGeranis/zephyrus.git
cd zephyrus
docker compose up
```

This pulls the released images from GitHub Container Registry. Set
`ZEPHYRUS_VERSION=0.1.0` to pin a version, or build from your checkout
instead:

```bash
docker compose -f docker-compose.yml -f docker-compose.build.yml up --build
```

Open <http://localhost:3000> and sign in with one of the demo team tokens:

| Token | Member | Can approve |
|-------|--------|-------------|
| `demo-pm` | Pat (PM/EM) | PRDs, task breakdowns |
| `demo-tech-lead` | Taylor (Tech Lead) | ADRs, task breakdowns, pull requests, workflows |
| `demo-qa` | Quinn (QA) | Test results |
| `demo-all` | Demo (all roles) | Everything — handy for a quick walkthrough |

The demo starts with a sample project: one feature whose PRD is waiting for the
PM, and one at the idea stage. Sign in as different members to see the approval
gates enforce who may approve what.

**Traces.** Jaeger runs alongside, at <http://localhost:16686>. Pick the
`zephyrus-api` service to see each request, the agent jobs it queued, their
model calls and database queries as one trace. See [Tracing and logs](#tracing-and-logs).

**What demo mode does.** Agents return canned output instead of calling Claude,
and artifacts are written to a folder inside the API container instead of to
GitHub. You can browse them with
`docker compose exec api ls -R /data/demo-repos`. Data survives restarts;
`docker compose down -v` resets everything.

**Keep it local.** The demo tokens are public. The ports are bound to
`127.0.0.1` so the stack is not reachable from your network; do not change that
while demo mode is on. To run Zephyrus for a team, with HTTPS and backups, follow
the [production guide](docs/production.md).

### Using real models and GitHub

Out of demo mode, agents run on a local `llama3.1:8b` served by Ollama, and
fall back to Claude Opus 5.5 (see [Choosing models](#choosing-models)). Start
the bundled Ollama with the `ollama` profile and pull the model once:

```bash
docker compose --profile ollama up -d
```

```bash
docker compose exec ollama ollama pull llama3.1:8b
```

Then create a `.env` file next to `docker-compose.yml`:

```bash
ZEPHYRUS_DEMO=false
CLAUDE_API_KEY=sk-ant-...        # for the Claude fallback; leave empty to run local-only
GITHUB_WEBHOOK_SECRET=...        # optional, for merge and deployment events
POSTGRES_PASSWORD=...            # choose one before the first start
PM_TOKEN=...                     # generate each with: openssl rand -hex 32
TECH_LEAD_TOKEN=...
QA_TOKEN=...
ALL_ROLES_TOKEN=...
```

The API refuses to start with `demo-` tokens when demo mode is off. Each
project's GitHub token is entered in the UI when you create the project, or
left empty when the Zephyrus GitHub App is installed on its repository.

### Using a GitHub App (recommended)

A GitHub App gives Zephyrus short-lived tokens, scoped to the repositories you
install it on, instead of long-lived personal tokens stored per project.

1. Create a GitHub App (Settings → Developer settings → GitHub Apps) with
   these repository permissions: **Contents**, **Issues** and **Pull
   requests**: read and write; **Metadata**: read.
2. For merge and deployment events, set its webhook URL to
   `https://<your-api>/api/webhooks/github`, its secret to the same value as
   `GITHUB_WEBHOOK_SECRET`, and subscribe to **Pull request** and
   **Deployment status**.
3. Generate a private key, and install the App on your repositories.
4. Add to `.env`:

   ```bash
   GITHUB_APP_ID=123456
   GITHUB_APP_PRIVATE_KEY="-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----"
   ```

For each repository the App is installed on, Zephyrus uses the App's tokens.
For any other repository it uses the project's stored token. An invalid key
stops the API at startup.

## Choosing models

Each agent runs on a **model chain**: a primary model and an optional fallback.
Models are written `provider:model`:

- `local:<model>`: any server with an OpenAI-compatible API, at
  `LocalModel:BaseUrl`. That covers Ollama, LM Studio, vLLM and llama.cpp's
  server.
- `claude:<model>`: the Claude API, using `Claude:ApiKey`.

The default chain is `local:llama3.1:8b`, falling back to `claude:claude-opus-5-5`.
Each pipeline stage can use its own chain, so you can keep cheap stages local
and send the demanding ones to Claude:

```json
"LanguageModel": {
  "Default": { "Primary": "local:llama3.1:8b", "Fallback": "claude:claude-opus-5-5" },
  "Stages": {
    "Code": { "Primary": "claude:claude-opus-5-5", "Fallback": "none" },
    "Task": { "Primary": "claude:claude-sonnet-5-5" }
  }
}
```

Stages are `Prd`, `Architect`, `Task`, `Code`, `Qa` and `DevOps`. A stage that
leaves a field out takes it from `Default`; `"Fallback": "none"` turns the
fallback off. A misspelt stage or model stops the API at startup.

**When the fallback is used.**

- If the primary model cannot be reached or returns an error, the call goes to
  the fallback straight away.
- If an agent job fails for any reason, including malformed output from a small
  model, its one automatic retry runs on the fallback.

Every agent run records the model that actually answered, for example
`llama3.1:8b → claude-opus-5-5` when a run fell back.

**Per project.** A Tech Lead can choose different models for any stage of one
project on its page, under **Models**: for example Claude for one client's
Code stage, while another project keeps everything local. Everyone on the
team can see the choices; each change is recorded with who made it, and
applies from the next agent run. A stage with no choice uses the configuration
above. In demo mode the demo model answers every stage, whatever is chosen.

**Costs.** Each agent run stores its input and output tokens and an estimated
cost. The feature page shows each run's cost and the total for the feature.
Local models cost $0. Claude prices come from the `ModelPricing` table in
`appsettings.json`, so update it when prices change. A model missing from the
table is shown as "price unknown".

**Local models: what to expect.**

- **Strict output.** The Task, Code and QA agents must return strict JSON. An
  8B model often doesn't, which is what the retry-on-fallback is for. Point the
  Code stage at a stronger model if it fails often.
- **Context length.** Agent prompts are long. The bundled Ollama raises its
  context window with `OLLAMA_CONTEXT_LENGTH=16384`; set the same on your own
  Ollama server, or answers will be cut off.

Claude requests opt into Anthropic's server-side refusal fallback
(`Claude:ServerSideFallback`). If Claude's safety classifiers decline a request,
the API re-runs it on Anthropic's recommended fallback model instead of failing.

## Tracing and logs

The API emits OpenTelemetry traces when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
The Docker setup sets it to the bundled Jaeger. Any OTLP backend works, such as
Grafana Tempo, Honeycomb or Datadog: point the variable at it.

![An approval request in Jaeger: the request, the agent job it queued and that job's model call in one trace](docs/images/tracing.png)

- **One trace per action.** Approving an artifact, the agent job it queues, and
  that job's model calls and database queries share a trace. The job runs
  later, in the background, so it stores the request's trace context and
  continues it.
- **Model calls** follow the OpenTelemetry generative-AI conventions: model
  requested and model that answered, input and output tokens. A fallback shows
  up as a `gen_ai.fallback` event.
- **Logs** are JSON lines in Docker
  (`Logging__Console__FormatterName=json`). Each line carries its `TraceId` and
  `SpanId`, so you can go from a log line to its trace.

## Screenshots

| The gate checks the approver's role | Every run and transition is recorded |
|---|---|
| ![A QA member is refused when approving an ADR: it needs the Tech Lead role](docs/images/approval-gate.png) | ![Agent jobs, agent runs with model, tokens and cost, and a timeline of who approved each stage](docs/images/audit-trail.png) |
| **The Tech Lead chooses models per project** | **Each run keeps its model and prompt version** |
| ![The Models section: each pipeline stage's model, one chosen by the Tech Lead with their name and date](docs/images/models.png) | ![An agent run: the model that answered, the prompt version and the full system prompt](docs/images/agent-run.png) |

## Getting Started (from source)

### Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0)
- [Node.js 26](https://nodejs.org/)
- [PostgreSQL 18](https://www.postgresql.org/) (or Docker)

### 1. Clone the repository

```bash
git clone https://github.com/MichaelGeranis/zephyrus.git
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
  "Claude": {
    "ApiKey": "sk-ant-your_claude_api_key"
  },
  "Team": {
    "Members": [
      {
        "Email": "you@example.com",
        "DisplayName": "You",
        "Token": "choose-a-long-random-token",
        "Roles": ["PmEm", "TechLead", "Qa"]
      }
    ]
  }
}
```

Every API call needs a team token: sign in from the header of the web UI with
the `Token` you set here. Generate one with `openssl rand -hex 32`. The GitHub
token for each repository is entered when you create a project in the UI, and
is stored encrypted.

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

The web UI has component tests, which mock the API:

```bash
cd src/Zephyrus.Web
npm test
```

And end-to-end tests, which drive a browser through the demo stack. Start a
fresh stack first, because the tests approve the demo PRD:

```bash
docker compose down -v && docker compose -f docker-compose.yml -f docker-compose.build.yml up --build -d
```

```bash
cd src/Zephyrus.Web
npx playwright install chromium
npm run test:e2e
```

### Configuration reference

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string (required — `appsettings.json` ships without one; `appsettings.Development.json` has a local default) |
| `Cors__AllowedOrigins__0` | Browser origin of the deployed frontend allowed to call the API (default: `http://localhost:3000`) |
| `GitHub__Webhook__Secret` | Shared secret for verifying inbound GitHub webhooks |
| `GitHub__App__AppId` | Zephyrus GitHub App ID; unset means every project uses its own token |
| `GitHub__App__PrivateKey` / `GitHub__App__PrivateKeyPath` | The App's private key (PEM, `\n` allowed for newlines), or a file holding it |
| `DataProtection__KeyPath` | Directory holding the key ring that encrypts stored project tokens — must persist across restarts |
| `LanguageModel__Default__Primary` | Primary model as `provider:model` (default: `local:llama3.1:8b`) |
| `LanguageModel__Default__Fallback` | Fallback model, or `none` (default: `claude:claude-opus-5-5`) |
| `LanguageModel__Stages__<Stage>__Primary` / `__Fallback` | Per-stage override, see [Choosing models](#choosing-models) |
| `LocalModel__BaseUrl` | OpenAI-compatible server for `local:` models (default: `http://localhost:11434/v1`) |
| `LocalModel__ApiKey` | Bearer key, for servers that require one (Ollama does not) |
| `LocalModel__MaxTokens` | Max output tokens for local models (default: `8192`) |
| `LocalModel__Timeout` | Per-call timeout for local models (default: `00:10:00`) |
| `Claude__ApiKey` | Anthropic API key |
| `Claude__MaxTokens` | Max output tokens, thinking included (default: `16000`) |
| `Claude__Effort` | `low`, `medium`, `high`, `xhigh` or `max` (default: `high`) |
| `Claude__ServerSideFallback` | Re-run requests Claude declines on Anthropic's recommended fallback (default: `true`) |
| `ModelPricing__Models__<model>__InputPerMillion` / `__OutputPerMillion` | Dollar prices for cost estimates |
| `Demo__Enabled` | Replace Claude and GitHub with local stand-ins and create sample data (default: `false`; the Docker setup defaults it to `true`) |
| `Demo__RepositoryRoot` | Folder the demo code host writes to (default: `demo-repos`) |
| `ReverseProxy__Enabled` | Read the client address and scheme from `X-Forwarded-For` and `X-Forwarded-Proto` (default: `false`); see the [production guide](docs/production.md#3-put-it-behind-a-reverse-proxy-with-tls) |
| `ReverseProxy__KnownProxies__0` / `ReverseProxy__KnownNetworks__0` | Proxy addresses, or CIDR ranges, whose forwarded headers are trusted; one is required when enabled |
| `Database__MigrateOnStartup` | Apply pending database migrations when the API starts (default: `false`; the Docker setup sets it) |
| `AgentJobs__PollInterval` | How often the job worker looks for due jobs (default: `00:00:05`) |
| `AgentJobs__Lease` | How long a worker holds a running job before another may take it over (default: `00:30:00`) |
| `AgentJobs__RetryDelay` | Wait before retrying a failed agent job (default: `00:00:30`) |
| `AgentRuns__ContentRetentionDays` | Days after which an agent run's input and output are removed, keeping the run's record (default: `0`, keep) |
| `Prompts__Directory` | Folder holding the agent prompts (default: `prompts` next to the API build) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Send traces to this OTLP endpoint, e.g. `http://jaeger:4317`; unset means tracing is off |
| `OTEL_SERVICE_NAME` | Service name on traces (default: `zephyrus-api`) |
| `NEXT_PUBLIC_API_URL` | Backend API URL for the frontend (default: `http://localhost:5000`) |
| `Team__Members__0__Email` | Team member identifier, recorded as the approver |
| `Team__Members__0__DisplayName` | Team member display name |
| `Team__Members__0__Token` | Bearer token that authenticates this member, as written; prefer `TokenHash` |
| `Team__Members__0__TokenHash` | SHA-256 of the member's token, as hex (`printf %s "$TOKEN" \| sha256sum`); used instead of `Token`, so the configuration holds no usable token |
| `Team__Members__0__Roles__0` | A role held by this member: `PmEm`, `TechLead`, or `Qa` |
| `Authentication__FailedSignIns__Limit` | Wrong team tokens one address may send before it is refused (default: `10`) |
| `Authentication__FailedSignIns__Window` | How long those are counted, and how long a refused address waits (default: `00:15:00`) |

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) for setup,
the architecture rules and how to submit a change. Every commit must be signed
off under the [Developer Certificate of Origin](https://developercertificate.org/).

This project follows a [Code of Conduct](CODE_OF_CONDUCT.md). To report a
security problem, see [SECURITY.md](SECURITY.md) — please do not open a public
issue.

## License

Zephyrus is licensed under the [Apache License 2.0](LICENSE).
