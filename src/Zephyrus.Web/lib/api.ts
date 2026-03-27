const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options?.headers,
    },
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Request failed: ${res.status}`);
  }

  return res.json();
}

import type { Project, Feature, Artifact, TaskItem, PipelineEvent, AgentInvocationSummary, AgentInvocationDetail } from "./types";

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
  approveArtifact: (featureId: string, artifactId: string, approvedBy: string) =>
    request<Artifact>(
      `/api/features/${featureId}/artifacts/${artifactId}/approve`,
      {
        method: "POST",
        body: JSON.stringify({ approvedBy }),
      }
    ),
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
};
