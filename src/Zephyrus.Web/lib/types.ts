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
};
