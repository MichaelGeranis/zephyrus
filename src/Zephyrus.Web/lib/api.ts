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

import type { Project, Feature, Artifact } from "./types";

export const api = {
  // Projects
  getProjects: () => request<Project[]>("/api/projects"),
  getProject: (id: string) => request<Project>(`/api/projects/${id}`),

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

  // PRD Generation
  generatePrd: (featureId: string) =>
    request<Artifact>(`/api/features/${featureId}/generate-prd`, {
      method: "POST",
    }),
};
