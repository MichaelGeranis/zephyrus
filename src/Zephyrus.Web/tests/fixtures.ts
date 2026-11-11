import type { AgentJob, Artifact, CurrentUser } from "@/lib/types";

export function artifact(overrides: Partial<Artifact> = {}): Artifact {
  return {
    id: "artifact-1",
    featureId: "feature-1",
    type: "Prd",
    repositoryPath: "docs/prd-invoices.md",
    approvedBy: null,
    approvedAt: null,
    commitSucceeded: true,
    ...overrides,
  };
}

export function agentJob(overrides: Partial<AgentJob> = {}): AgentJob {
  return {
    id: "job-1",
    kind: "Architect",
    status: "Queued",
    attempts: 0,
    maxAttempts: 2,
    lastError: null,
    enqueuedAt: "2026-11-09T10:00:00Z",
    nextAttemptAt: "2026-11-09T10:00:00Z",
    startedAt: null,
    completedAt: null,
    ...overrides,
  };
}

export const pm: CurrentUser = { email: "pat@example.com", displayName: "Pat", roles: ["PmEm"] };
