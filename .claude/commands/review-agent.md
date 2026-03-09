# /review-agent

Review an existing agent implementation against Zephyrus architecture rules.

Agent to review: $ARGUMENTS
(e.g. PrdAgent or path to file)

## Instructions

Read ARCHITECTURE.md fully before reviewing.
Check the agent against every rule below.
Report each finding as: PASS / FAIL / WARN with a brief explanation.
For every FAIL, provide the corrected code snippet.

---

## Review Checklist

### Layer Compliance
- [ ] Input record is defined in `Zephyrus.Core/Agents/` (not Infrastructure)
- [ ] Output record is defined in `Zephyrus.Core/Agents/` (not Infrastructure)
- [ ] Agent implementation is in `Zephyrus.Infrastructure/AI/Agents/`
- [ ] Agent implements `IAgent<TInput, TOutput>` from Zephyrus.Core

### Statelessness
- [ ] No mutable instance fields (only readonly injected dependencies)
- [ ] No static state
- [ ] Safe to retry — calling RunAsync twice produces the same artifact

### Dependencies
- [ ] Uses `ILanguageModel` — not raw `HttpClient` or `HttpClientFactory` directly
- [ ] Uses `ICodeHost` — not Octokit types directly
- [ ] Uses `IPromptLoader` — system prompt not hardcoded as string literal
- [ ] No reference to `ZephyrusDbContext` or any EF Core types

### System Prompt
- [ ] Loaded from `/prompts/{agentname}.md` at runtime
- [ ] Not hardcoded inline in the agent class
- [ ] Prompt file exists at the correct path

### Output
- [ ] Artifact committed to GitHub via `ICodeHost.CommitFileAsync()`
- [ ] Commit message follows format: `[Zephyrus] Add {agentname} artifact`
- [ ] GitHub path follows convention: `docs/{agentname}-{feature-slug}.md`
- [ ] Output record has `Success` and `ErrorMessage` fields

### Error Handling
- [ ] LLM failures are not silently swallowed
- [ ] GitHub commit failures are not silently swallowed
- [ ] Errors surface to the Orchestrator for retry logic

### Testing
- [ ] Unit test exists in `Zephyrus.Tests/Infrastructure/AI/`
- [ ] Test mocks `ILanguageModel`, `ICodeHost`, `IPromptLoader`
- [ ] Test covers: success path
- [ ] Test covers: LLM failure path
- [ ] No real HTTP calls in unit tests

### Naming
- [ ] Class name follows `{Name}Agent` convention
- [ ] Input record follows `{Name}AgentInput` convention
- [ ] Output record follows `{Name}AgentOutput` convention
- [ ] Uses ubiquitous language (Feature, Artifact, Constitution — not synonyms)

---

## Output Format

Report as:

```
REVIEWING: {AgentName}

LAYER COMPLIANCE
  ✅ Input record in Zephyrus.Core
  ❌ Output record in Zephyrus.Infrastructure — FAIL
     Fix: Move PrdAgentOutput.cs to Zephyrus.Core/Agents/

STATELESSNESS
  ✅ No mutable instance fields

DEPENDENCIES
  ⚠️  HttpClient injected directly — WARN
     Prefer ILanguageModel abstraction for testability
     Fix: ...

SUMMARY
  PASS: 12 / WARN: 1 / FAIL: 1
  Action required before merging: Yes
```
