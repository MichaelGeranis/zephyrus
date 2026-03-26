export interface Project {
  id: string;
  name: string;
  description: string;
  repositorySlug: string;
  createdAt: string;
}

export interface Feature {
  id: string;
  projectId: string;
  prompt: string;
  status: string;
  createdAt: string;
}

export interface Artifact {
  id: string;
  featureId: string;
  type: string;
  repositoryPath: string;
  approvedBy: string | null;
  approvedAt: string | null;
  commitSucceeded: boolean;
}

export interface TaskItem {
  id: string;
  featureId: string;
  title: string;
  status: string;
  agentType: string;
  externalIssueId: number | null;
  prId: number | null;
}

export interface PipelineEvent {
  id: string;
  featureId: string;
  fromStatus: string;
  toStatus: string;
  triggeredBy: string;
  timestamp: string;
}

export interface AgentInvocationSummary {
  id: string;
  featureId: string;
  agentName: string;
  invokedAt: string;
  durationMs: number;
}

export interface AgentInvocationDetail extends AgentInvocationSummary {
  systemPrompt: string;
  userMessage: string;
  response: string;
}

export const PIPELINE_STAGES = [
  "Ideation",
  "PrdPending",
  "PrdApproved",
  "ArchPending",
  "ArchApproved",
  "TasksPending",
  "TasksApproved",
  "Coding",
  "QaPending",
  "QaApproved",
  "Deployed",
] as const;

export type FeatureStatus = (typeof PIPELINE_STAGES)[number];

export const APPROVABLE_STATUSES: Record<string, string> = {
  PrdPending: "Prd",
  ArchPending: "Adr",
  TasksPending: "Task",
  Coding: "Pr",
  QaPending: "Test",
  QaApproved: "Workflow",
};

/** Human-readable labels for each pipeline stage. */
export const STAGE_LABELS: Record<string, string> = {
  Ideation: "Ideation",
  PrdPending: "PRD Pending",
  PrdApproved: "PRD Approved",
  ArchPending: "Architecture Pending",
  ArchApproved: "Architecture Approved",
  TasksPending: "Tasks Pending",
  TasksApproved: "Tasks Approved",
  Coding: "Coding",
  QaPending: "QA Pending",
  QaApproved: "QA Approved",
  Deployed: "Deployed",
};
