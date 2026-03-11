# Zephyrus — Business Concepts

This document defines the business rules, domain concepts, and use cases of Zephyrus.
For technical architecture and implementation details, see [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Core Concept

Zephyrus automates software delivery for small teams by placing **AI agents at every execution step** and **humans at every validation gate**. The product exists because a 3-person team cannot manually write PRDs, architecture docs, task breakdowns, code, tests, and CI/CD pipelines for every feature — but they *can* review and approve each of those outputs.

The fundamental contract: **AI executes, humans validate. Nothing ships without human approval.**

---

## The 3-Person Team Model

Zephyrus is designed for a team of three amplified roles:

| Role | Abbreviation | Domain | Approval Responsibilities |
|------|-------------|--------|--------------------------|
| Product & Engineering Manager | PM/EM | Vision & Delivery | Feature prompts, PRD approval, task scope approval |
| Tech Lead | TL | Architecture & Code | ADR approval, PR review, task ordering approval, deployment merge |
| QA Engineer | QA | Quality & Correctness | Test result approval, acceptance criteria definition |

**Rules:**
- Each role must retain enough technical knowledge to troubleshoot and override AI output.
- No artifact advances without explicit approval from the designated role.
- A single person can hold multiple roles in smaller teams, but the approval responsibilities remain distinct.

---

## Domain Model

### Project

A software project that Zephyrus manages.

| Property | Business meaning |
|----------|------------------|
| Name | Human-readable project name |
| Description | What this project does |
| Config | The **Project Constitution** — the shared context every agent reads before acting |
| RepositorySlug | GitHub `owner/repo` where all artifacts live |

**Rules:**
- Every project has exactly one Project Constitution.
- Every agent receives the constitution as part of its input. It is the baton passed between all agents.
- GitHub is the source of truth for all code and document artifacts. The database stores paths, not contents.

### Feature

A unit of work that flows through the delivery pipeline. Represents a single idea from inception to deployment.

| Property | Business meaning |
|----------|------------------|
| Prompt | The original idea, written in natural language by the PM/EM |
| Status | Current position in the pipeline (see State Machine below) |

**Rules:**
- A feature always starts in `Ideation` status.
- A feature can only move forward through the pipeline — never backward.
- Status transitions are governed by the deterministic state machine. No exceptions.
- A feature's prompt is immutable after creation. Refinement happens in the PRD, not by editing the prompt.

### Artifact

An output produced by an agent at a pipeline stage. The concrete deliverable that humans review.

| Property | Business meaning |
|----------|------------------|
| Type | What kind of artifact: `Prd`, `Adr`, `Task`, `Pr`, `Test` |
| RepositoryPath | Where the artifact lives in the GitHub repo |
| ApprovedBy | Who approved it (null until approved) |
| ApprovedAt | When it was approved (null until approved) |

**Rules:**
- An artifact belongs to exactly one feature.
- An artifact can be approved exactly once. Double-approval is rejected.
- Artifact content lives in GitHub, not in the database. The database only stores the path.
- Approving an artifact advances the feature to the next pipeline status.

### TaskItem

A granular, implementable unit of work derived from the PRD and ADR. Maps to a GitHub Issue.

| Property | Business meaning |
|----------|------------------|
| Title | What needs to be done |
| AgentType | Which agent handles it: `BE`, `FE`, `DB`, `DevOps` |
| ExternalIssueId | The GitHub Issue number |
| PrId | The GitHub PR number (once code is generated) |
| Status | `Pending`, `InProgress`, `PrOpen`, `Done` |

**Rules:**
- Tasks are created by the Task Agent, not by humans.
- Each task maps to exactly one GitHub Issue.
- Tasks can be parallelized where dependencies allow.
- Task status is independent of feature status — a feature advances when all its tasks reach the required state.

### PipelineEvent

An audit log entry recording every state transition in the pipeline.

| Property | Business meaning |
|----------|------------------|
| FromStatus | Status before the transition |
| ToStatus | Status after the transition |
| TriggeredBy | Who or what caused the transition (`system` for agent invocations, user identifier for approvals) |
| Timestamp | When the transition occurred |

**Rules:**
- Every status change produces exactly one PipelineEvent. No silent transitions.
- Events are append-only. They are never modified or deleted.

### Deployment

A record of a feature being deployed to an environment.

| Property | Business meaning |
|----------|------------------|
| Sha | The Git commit SHA that was deployed |
| Environment | Target environment (e.g., `production`, `staging`) |
| Status | `Pending`, `Success`, `Failed` |

---

## The Pipeline

### State Machine

Every feature moves through these statuses in order. The state machine is deterministic and enforced in code.

```
Ideation
  |
  | [PM/EM submits feature prompt, triggers PRD Agent]
  v
PrdPending
  |
  | [PM/EM reviews and approves PRD artifact]
  v
PrdApproved
  |
  | [Orchestrator automatically triggers Architect Agent]
  v
ArchPending
  |
  | [Tech Lead reviews and approves ADR artifact]
  v
ArchApproved
  |
  | [Orchestrator automatically triggers Task Agent]
  v
TasksPending
  |
  | [PM/EM + Tech Lead review and approve task breakdown]
  v
TasksApproved
  |
  | [Orchestrator triggers Code Agents (one per task)]
  v
Coding
  |
  | [All PRs opened, Orchestrator triggers QA Agent]
  v
QaPending
  |
  | [QA reviews and approves test results]
  v
QaApproved
  |
  | [Tech Lead merges, CI/CD deploys]
  v
Deployed
```

**Rules:**
- Transitions are forward-only. A feature cannot go backward in the pipeline.
- Each transition is the only valid next step. There are no branches or alternative paths.

### Approval Preconditions

Each artifact type can only be approved when the feature is in a specific status:

| Artifact Type | Required Feature Status | Who Approves | Next Status After Approval |
|---------------|------------------------|-------------|---------------------------|
| Prd | PrdPending | PM/EM | PrdApproved |
| Adr | ArchPending | Tech Lead | ArchApproved |
| Task | TasksPending | PM/EM + Tech Lead | TasksApproved |
| Pr | Coding | Tech Lead | QaPending |
| Test | QaPending | QA | QaApproved |

**Rules:**
- Attempting to approve an artifact when the feature is not in the required status is rejected.
- Attempting to approve an artifact that is already approved is rejected.
- After approval, the orchestrator may automatically trigger the next agent.

---

## The Orchestrator

The orchestrator is a **deterministic state machine, not an AI**. Its job is to react to events and invoke the correct next agent.

### Trigger Map

| Event | Orchestrator Action |
|-------|-------------------|
| PRD approved (feature enters `PrdApproved`) | Invoke Architect Agent |
| ADR approved (feature enters `ArchApproved`) | Invoke Task Agent |
| Tasks approved (feature enters `TasksApproved`) | Invoke Code Agents (one per task) |
| All PRs opened (feature enters `Coding` complete) | Invoke QA Agent |
| Tests approved (feature enters `QaApproved`) | Merge PRs, trigger deployment |

**Rules:**
- The orchestrator never makes decisions. It follows the trigger map exactly.
- The orchestrator is the only component that invokes agents. Agents never invoke other agents.
- All orchestrator actions produce PipelineEvent audit entries.

---

## AI Agents

### General Agent Rules

1. **Agents are stateless.** They take typed input, produce typed output, and have no memory between invocations. All state lives in the database and GitHub.
2. **Every agent reads the Project Constitution.** It is always part of the input context.
3. **Agents commit their own artifacts.** The use case orchestrating the agent is responsible for committing the output to GitHub and recording the artifact in the database.
4. **Agents can be retried freely.** Because they are stateless, re-running an agent with the same input produces the same (or equivalent) output.
5. **Agent output is never trusted blindly.** Every agent output goes through a human approval gate before the pipeline advances.

### PRD Agent

- **Purpose**: Generate a Product Requirements Document from a feature idea.
- **Input**: Feature prompt (natural language) + Project Constitution.
- **Output**: Structured markdown with: Problem Statement, Target Users, Goals, Non-Goals, User Stories (Given/When/Then), Acceptance Criteria, Open Questions.
- **Artifact path**: `docs/prd-{feature-slug}.md`
- **Reviewer**: PM/EM

### Architect Agent

- **Purpose**: Generate an Architecture Decision Record from an approved PRD.
- **Input**: Approved PRD markdown + Project Constitution.
- **Output**: Structured markdown with: Summary, New Components (with layer assignments), Database Changes, API Contracts, External Dependencies, Sequence Diagram, Estimated Complexity, Risks.
- **Artifact path**: `docs/adr-{feature-slug}.md`
- **Reviewer**: Tech Lead

### Task Agent

- **Purpose**: Break down the approved PRD and ADR into atomic, implementable tasks.
- **Input**: Approved PRD + Approved ADR + Project Constitution.
- **Output**: Ordered list of tasks, each with: Title, Context, Acceptance Criteria, Dependencies, Agent Type (BE/FE/DB/DevOps).
- **Artifacts**: GitHub Issues (one per task), labeled and milestoned.
- **Reviewer**: PM/EM + Tech Lead

### Code Agent

- **Purpose**: Implement a single task as code.
- **Input**: Single GitHub Issue + ADR + Project Constitution + relevant existing code.
- **Output**: Code changes satisfying the issue.
- **Artifacts**: Feature branch (`feature/{slug}/{task-id}`) + Pull Request linked to the Issue.
- **Reviewer**: Tech Lead
- **Note**: One invocation per task. Parallelizable where task dependencies allow.

### QA Agent

- **Purpose**: Write tests and validate code changes.
- **Input**: PR diff + original Issue + acceptance criteria + Project Constitution.
- **Output**: Unit tests, integration tests, test run summary (pass/fail/coverage %), failure annotations.
- **Artifacts**: Test files committed to PR branch + CI run report as PR comment.
- **Reviewer**: QA

### DevOps Agent

- **Purpose**: Generate CI/CD pipeline configuration.
- **Input**: Project Constitution + deployment target.
- **Output**: GitHub Actions workflow file.
- **Artifact**: `.github/workflows/deploy.yml`
- **Reviewer**: Tech Lead
- **Note**: Runs once at project setup, then on configuration changes.

---

## The Project Constitution

The Project Constitution is a YAML configuration file that defines the ground rules for a project. Every agent reads it before producing output.

It contains:
- **Project identity**: name, description
- **Stack choices**: frontend framework, backend framework, database, ORM
- **Conventions**: linting rules, testing framework, branch strategy, commit style
- **Deployment**: target platform, trigger, environment variables
- **Architecture patterns**: data access pattern, API style, auth strategy

**Rules:**
- The constitution is written once by the Tech Lead at project creation.
- It is stored as a string in the `Project.Config` field.
- Changing the constitution may require re-running agents for consistency.
- The constitution is the primary mechanism for ensuring agent output consistency across the pipeline.

---

## Error Handling and Retry Policy

| Scenario | Behavior |
|----------|----------|
| Agent produces invalid output | Retry once with error context injected into the prompt |
| Agent fails after one retry | Escalate to human (feature stays in current status) |
| Approval attempted on wrong status | Rejected with descriptive error message |
| Approval attempted on already-approved artifact | Rejected with descriptive error message |
| Artifact not found | 404 response |
| Feature not found | 404 response |

**Rules:**
- Agents are never retried infinitely. Maximum one automatic retry.
- Failed state transitions do not corrupt the pipeline. The feature remains in its current status.
- All errors are logged as PipelineEvents where applicable.

---

## Glossary

| Term | Definition |
|------|-----------|
| **Artifact** | A concrete output from an agent: a PRD document, ADR document, GitHub Issue, PR, or test suite |
| **Approval Gate** | A human review checkpoint. Nothing advances without explicit approval |
| **Constitution** | The Project Constitution — a YAML config that every agent reads for project context |
| **Feature** | A unit of work representing a single idea moving through the pipeline |
| **Orchestrator** | The deterministic state machine that wires approval events to agent invocations |
| **Pipeline** | The ordered sequence of stages a feature passes through from idea to deployment |
| **PipelineEvent** | An audit log entry recording a state transition |
| **Slug** | A URL-safe, lowercase, hyphenated version of a feature prompt (e.g., `add-user-authentication`) |
| **State Machine** | The `PipelineStateMachine` that enforces valid transitions between feature statuses |

## Ubiquitous Language

These terms must be used consistently across all code, comments, and variable names.

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
