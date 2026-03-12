"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { api } from "@/lib/api";
import { StatusBadge, PipelineProgress } from "@/components/StatusBadge";
import type { Project, Feature } from "@/lib/types";

export default function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [project, setProject] = useState<Project | null>(null);
  const [features, setFeatures] = useState<Feature[]>([]);
  const [loading, setLoading] = useState(true);
  const [prompt, setPrompt] = useState("");
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function loadData() {
    Promise.all([api.getProject(id), api.getFeaturesByProject(id)])
      .then(([p, f]) => {
        setProject(p);
        setFeatures(f);
      })
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    loadData();
  }, [id]);

  async function handleCreateFeature(e: React.FormEvent) {
    e.preventDefault();
    if (!prompt.trim()) return;

    setCreating(true);
    setError(null);
    try {
      await api.createFeature(id, prompt.trim());
      setPrompt("");
      loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create feature.");
    } finally {
      setCreating(false);
    }
  }

  if (loading) return <p className="text-gray-500">Loading...</p>;
  if (!project) return <p className="text-red-500">Project not found.</p>;

  return (
    <div>
      <div className="mb-8">
        <Link href="/projects" className="text-sm text-blue-600 hover:text-blue-800">
          &larr; Projects
        </Link>
        <h1 className="text-2xl font-bold text-gray-900 mt-2">{project.name}</h1>
        <p className="text-gray-500 mt-1">{project.description}</p>
      </div>

      {/* Create feature form */}
      <div className="bg-white rounded-lg border border-gray-200 p-4 mb-6">
        <h2 className="text-sm font-semibold text-gray-700 mb-3">New Feature</h2>
        <form onSubmit={handleCreateFeature} className="flex gap-3">
          <input
            type="text"
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
            placeholder="Describe the feature idea..."
            className="flex-1 px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          />
          <button
            type="submit"
            disabled={creating || !prompt.trim()}
            className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 disabled:opacity-50"
          >
            {creating ? "Creating..." : "Create"}
          </button>
        </form>
        {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
      </div>

      <h2 className="text-lg font-semibold text-gray-900 mb-4">Features</h2>

      {features.length === 0 ? (
        <p className="text-gray-500">No features yet. Create one above.</p>
      ) : (
        <div className="grid gap-3">
          {features.map((feature) => (
            <Link
              key={feature.id}
              href={`/features/${feature.id}`}
              className="block p-4 bg-white rounded-lg border border-gray-200 hover:border-blue-300 transition-colors"
            >
              <div className="flex items-start justify-between gap-4">
                <div className="flex-1 min-w-0">
                  <p className="text-sm text-gray-900 truncate">{feature.prompt}</p>
                  <div className="mt-2">
                    <PipelineProgress status={feature.status} />
                  </div>
                </div>
                <StatusBadge status={feature.status} />
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
