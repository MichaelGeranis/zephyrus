# Zephyrus

Like the west wind that nourishes the Elysian Fields, Zephyrus breathes life into ideas 
turning prompts into PRDs, architecture, code, tests, and deployed software through a
pipeline of specialized AI agents with human validation at every gate.

Zephyrus is an AI-powered software delivery platform that orchestrates the full journey
from idea to production. It is designed for a 3-person amplified team that produces
the output of 10, by placing AI agents at every execution step and humans at every
validation gate.

```
Idea (prompt) --> PRD --> Architecture --> Tasks --> Code --> QA --> Deploy
```

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

## Architecture [here](ARCHITECTURE.md)

## Business rules [here](BUSINESS.md)

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

## Contributing

### Development workflow

1. **Fork** and clone the repository.
2. Create a feature branch: `git checkout -b feat/your-feature`.
3. Make changes following the architecture rules below.
4. Run tests: `dotnet test`.
5. Commit using [conventional commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `refactor:`, etc.
6. Open a pull request against `main`.

### Environment variables

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `GitHub__Token` | GitHub personal access token |
| `Claude__ApiKey` | Anthropic API key |
| `Claude__Model` | Claude model ID (default: `claude-sonnet-4-20250514`) |
| `Claude__MaxTokens` | Max output tokens (default: `4096`) |
| `NEXT_PUBLIC_API_URL` | Backend API URL for the frontend (default: `http://localhost:5000`) |
| `Team__Members__0__Email` | Team member identifier, recorded as the approver |
| `Team__Members__0__DisplayName` | Team member display name |
| `Team__Members__0__Token` | Bearer token that authenticates this member |
| `Team__Members__0__Roles__0` | A role held by this member: `PmEm`, `TechLead`, or `Qa` |

---

## License

TBD
