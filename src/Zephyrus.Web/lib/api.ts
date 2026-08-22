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
    const text = await res.text();
    throw new Error(text || `Request failed: ${res.status}`);
  }

  return res.json();
}

import type { Project, Feature, Artifact, TaskItem, PipelineEvent, AgentInvocationSummary, AgentInvocationDetail, CurrentUser } from "./types";

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

  // Rerun step
  rerunStep: (featureId: string, step?: string) =>
    request<Feature>(`/api/features/${featureId}/rerun-step`, {
      method: "POST",
      body: step ? JSON.stringify({ step }) : undefined,
    }),

  // PRD Generation
  generatePrd: (featureId: string) =>
    request<Artifact>(`/api/features/${featureId}/generate-prd`, {
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
