# Contributing to Zephyrus

Thanks for your interest in Zephyrus. This guide covers how to set up the
project, the rules the codebase follows, and how to get a change merged.

By participating you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).
To report a security problem, follow [SECURITY.md](SECURITY.md) instead of
opening a public issue.

## Before you start

- **Bugs and small fixes:** open a pull request directly, or an issue first if
  you are unsure it is a bug.
- **Features and larger changes:** open an issue to discuss the approach first.
  Search [existing issues](https://github.com/MichaelGeranis/zephyrus/issues)
  to see whether it is already planned or discussed.

## Development setup

Prerequisites: [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0),
[Node.js 26](https://nodejs.org/) and
[PostgreSQL 18](https://www.postgresql.org/) (or Docker).

```bash
dotnet build Zephyrus.sln                 # build the backend
dotnet test                               # run unit and integration tests
cd src/Zephyrus.Web && npm install        # install frontend dependencies
```

The tests need no database, GitHub token or Claude API key: they use SQLite
in-memory and fakes. To try the full app, `docker compose up` starts it
in demo mode with the released images; see the [README](README.md#run-with-docker).
To try your own changes in it, build from your checkout:
`docker compose -f docker-compose.yml -f docker-compose.build.yml up --build`.

## Architecture rules

Zephyrus follows Clean Architecture. Read [ARCHITECTURE.md](ARCHITECTURE.md)
before making structural changes. The rules reviewers check most often:

- **The dependency rule is absolute.** `Core` depends on nothing,
  `Application` depends only on `Core`, `Infrastructure` implements `Core`
  interfaces and never references `Application`.
- **`Zephyrus.Core` has zero NuGet packages.**
- **No business logic in controllers or in the frontend.** Controllers map
  HTTP to use cases; `Zephyrus.Web` renders API responses.
- **The state machine governs every status change.** Never set a feature's
  status directly.
- **Agents are stateless.** All state lives in the database and GitHub.
- **External systems stay in `Infrastructure`.** No `HttpClient`,
  `DbContext` or Octokit types outside it.

Use the domain's ubiquitous language (`Feature`, `Artifact`, `Pipeline`,
`Agent`, …) as defined in [BUSINESS.md](BUSINESS.md).

## Making a change

1. Fork the repository and create a branch from `main`:
   `feat/short-description`, `fix/issue-number`, `docs/topic`, etc.
2. Make the change, with tests:
   - unit tests for business logic, integration tests for API endpoints;
   - no test may make a real HTTP or database call;
   - name tests `{MethodName}_When{Condition}_Should{ExpectedResult}`.
3. Run the checks:

   ```bash
   dotnet test
   dotnet format
   cd src/Zephyrus.Web && npm run lint && npm run typecheck && npm run test:coverage
   ```

   CI fails below 80% line coverage: on the backend, the unit and integration
   tests together (`scripts/coverage-gate.py`, leaving out migrations and
   generated code); on the frontend, components, `lib/` and the proxy. To see
   the backend figure locally:

   ```bash
   for suite in Unit Integration; do dotnet test --project tests/Zephyrus.${suite}Tests --coverage --coverage-settings "$PWD/coverage.settings.xml" --coverage-output-format cobertura --coverage-output "$PWD/TestResults/coverage/$suite.cobertura.xml"; done
   python3 scripts/coverage-gate.py 80 TestResults/coverage/*.cobertura.xml
   ```

4. Commit using [Conventional Commits](https://www.conventionalcommits.org/)
   (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`) and **sign off
   every commit** (see below).
5. Add a line under `Unreleased` in [CHANGELOG.md](CHANGELOG.md) for any
   user-visible change.
6. Open a pull request against `main` and fill in the template.

Pull requests are squash-merged to keep `main` linear. Rebase on `main` if
there are conflicts, and resolve all review conversations before merging.

## Developer Certificate of Origin (DCO)

Zephyrus does not use a contributor licence agreement. Instead, every commit
must be signed off to certify that you wrote it or otherwise have the right to
contribute it under the project's licence, as described in the
[Developer Certificate of Origin](https://developercertificate.org/).

Sign off by adding `-s` when committing:

```bash
git commit -s -m "fix: handle empty constitution"
```

This appends a line matching your git identity:

```
Signed-off-by: Your Name <you@example.com>
```

A CI check verifies that every commit in a pull request carries a sign-off
matching its author's email. To fix a pull request that fails it:

```bash
git rebase --signoff main
git push --force-with-lease
```

## Licence

By contributing, you agree that your contributions are licensed under the
[Apache License 2.0](LICENSE).
