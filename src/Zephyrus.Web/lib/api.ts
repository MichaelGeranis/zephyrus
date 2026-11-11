import { getTeamToken } from "./auth";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const token = getTeamToken();

  const res = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options?.headers,
    },
  });

  if (!res.ok) {
    throw new ApiError(res.status, errorMessage(res.status, await res.text()));
  }

  return res.json();
}

/**
 * Saves a file the API serves. The API needs the team token, so a plain link
 * would not do: fetch it, then hand the browser the bytes.
 */
async function download(path: string, fileName: string): Promise<void> {
  const token = getTeamToken();
  const res = await fetch(`${API_URL}${path}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });
  if (!res.ok) {
    throw new ApiError(res.status, errorMessage(res.status, await res.text()));
  }

  const url = URL.createObjectURL(await res.blob());
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

/** A failed API call; the message is the server's explanation. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/**
 * The API answers errors with problem details. Show their `detail`, and for
 * unexpected errors the trace ID the team can look up in the logs.
 */
function errorMessage(status: number, body: string): string {
  try {
    const problem = JSON.parse(body) as { detail?: unknown; traceId?: unknown };
    if (typeof problem.detail === "string" && problem.detail) {
      return typeof problem.traceId === "string" && status >= 500
        ? `${problem.detail} (trace ID: ${problem.traceId})`
        : problem.detail;
    }
  } catch {
    /* not JSON: fall back to the raw text */
  }
  return body || `Request failed: ${status}`;
}

import type { Project, Feature, Artifact, TaskItem, PipelineEvent, AgentInvocationSummary, AgentInvocationDetail, CurrentUser, AgentJob, FeatureUsage, ProjectModels } from "./types";

export interface DeletionPreview {
  entityTitle: string;
  childrenCount: number;
  warnings: string[];
}

export const api = {
  // Projects
  getProjects: () => request<Project[]>("/api/projects"),
  getProject: (id: string) => request<Project>(`/api/projects/${id}`),
  createProject: (name: string, description: string, config: string, repositorySlug: string, gitHubToken: string) =>
    request<Project>("/api/projects", {
      method: "POST",
      body: JSON.stringify({ name, description, config, repositorySlug, gitHubToken }),
    }),

  // Models per pipeline stage. The server decides who may change them.
  getProjectModels: (projectId: string) => request<ProjectModels>(`/api/projects/${projectId}/models`),
  chooseProjectModels: (projectId: string, stage: string, primary: string, fallback: string | null) =>
    request<ProjectModels>(`/api/projects/${projectId}/models/${stage}`, {
      method: "PUT",
      body: JSON.stringify({ primary, fallback }),
    }),
  resetProjectModels: (projectId: string, stage: string) =>
    request<ProjectModels>(`/api/projects/${projectId}/models/${stage}`, { method: "DELETE" }),

  // Audit trail, as a JSON file (format zephyrus.audit/v1)
  downloadFeatureAudit: (featureId: string) =>
    download(`/api/features/${featureId}/audit-export`, `zephyrus-audit-feature-${featureId}.json`),
  downloadProjectAudit: (projectId: string) =>
    download(`/api/projects/${projectId}/audit-export`, `zephyrus-audit-project-${projectId}.json`),

  // Features
  getFeature: (id: string) => request<Feature>(`/api/features/${id}`),
  getFeaturesByProject: (projectId: string) =>
    request<Feature[]>(`/api/features/by-project/${projectId}`),
  createFeature: (projectId: string, prompt: string) =>
    request<Feature>("/api/features", {
      method: "POST",
      body: JSON.stringify({ projectId, prompt }),
    }),

  // Artifacts
  getArtifacts: (featureId: string) =>
    request<Artifact[]>(`/api/features/${featureId}/artifacts`),
  getArtifactContent: (featureId: string, artifactId: string) =>
    request<{ content: string }>(
      `/api/features/${featureId}/artifacts/${artifactId}/content`
    ),
  // The approver is the authenticated caller — the server ignores anything
  // the client might claim, so no identity is sent.
  approveArtifact: (featureId: string, artifactId: string) =>
    request<Artifact>(
      `/api/features/${featureId}/artifacts/${artifactId}/approve`,
      { method: "POST" }
    ),

  getCurrentUser: () => request<CurrentUser>("/api/me"),
  updateArtifactContent: (featureId: string, artifactId: string, content: string) =>
    request<Artifact>(
      `/api/features/${featureId}/artifacts/${artifactId}/content`,
      {
        method: "PUT",
        body: JSON.stringify({ content }),
      }
    ),

  // Tasks
  getTasks: (featureId: string) =>
    request<TaskItem[]>(`/api/features/${featureId}/tasks`),

  // Pipeline Events
  getPipelineEvents: (featureId: string) =>
    request<PipelineEvent[]>(`/api/features/${featureId}/pipeline-events`),

  // Agent Invocations
  getFeatureUsage: (featureId: string) =>
    request<FeatureUsage>(`/api/features/${featureId}/usage`),
  getAgentJobs: (featureId: string) =>
    request<AgentJob[]>(`/api/features/${featureId}/agent-jobs`),
  getAgentInvocations: (featureId: string) =>
    request<AgentInvocationSummary[]>(`/api/features/${featureId}/agent-invocations`),
  getAgentInvocationDetail: (featureId: string, invocationId: string) =>
    request<AgentInvocationDetail>(`/api/features/${featureId}/agent-invocations/${invocationId}`),

  // Retry
  retryArtifactCommit: (featureId: string, artifactId: string) =>
    request<Artifact>(
      `/api/features/${featureId}/artifacts/${artifactId}/retry-commit`,
      { method: "POST" }
    ),

  // Rerun step: queues the agent (202); the feature page follows the job.
  rerunStep: (featureId: string, step?: string) =>
    request<Feature>(`/api/features/${featureId}/rerun-step`, {
      method: "POST",
      body: step ? JSON.stringify({ step }) : undefined,
    }),

  // PRD Generation
  // Queues the PRD Agent (202); the feature page follows the job.
  generatePrd: (featureId: string) =>
    request<Feature>(`/api/features/${featureId}/generate-prd`, {
      method: "POST",
    }),

  // Delete — Projects
  getProjectDeletionPreview: (id: string) =>
    request<DeletionPreview>(`/api/projects/${id}/deletion-preview`),
  deleteProject: (id: string) =>
    request<{ deletedEntitiesCount: number }>(`/api/projects/${id}`, {
      method: "DELETE",
    }),

  // Delete — Features
  getFeatureDeletionPreview: (id: string) =>
    request<DeletionPreview>(`/api/features/${id}/deletion-preview`),
  deleteFeature: (id: string) =>
    request<{ deletedEntitiesCount: number }>(`/api/features/${id}`, {
      method: "DELETE",
    }),

  // Delete — Artifacts
  getArtifactDeletionPreview: (featureId: string, artifactId: string) =>
    request<DeletionPreview>(
      `/api/features/${featureId}/artifacts/${artifactId}/deletion-preview`
    ),
  deleteArtifact: (featureId: string, artifactId: string) =>
    request<{ deletedEntitiesCount: number }>(
      `/api/features/${featureId}/artifacts/${artifactId}`,
      { method: "DELETE" }
    ),
};
