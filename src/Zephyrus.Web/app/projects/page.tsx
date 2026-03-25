"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import type { Project } from "@/lib/types";

export default function ProjectsPage() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [repositorySlug, setRepositorySlug] = useState("");
  const [config, setConfig] = useState("");

  function loadProjects() {
    api.getProjects().then(setProjects).finally(() => setLoading(false));
  }

  useEffect(() => {
    loadProjects();
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setCreating(true);
    setError(null);
    try {
      await api.createProject(name, description, config, repositorySlug);
      setName("");
      setDescription("");
      setRepositorySlug("");
      setConfig("");
      setShowForm(false);
      loadProjects();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create project.");
    } finally {
      setCreating(false);
    }
  }

  if (loading) return <p className="text-gray-500">Loading projects...</p>;

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Projects</h1>
        <button
          onClick={() => setShowForm(!showForm)}
          className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700"
        >
          {showForm ? "Cancel" : "New Project"}
        </button>
      </div>

      {showForm && (
        <form
          onSubmit={handleCreate}
          className="bg-white rounded-lg border border-gray-200 p-6 mb-6 space-y-4"
        >
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Name
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              placeholder="My Project"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Description
            </label>
            <input
              type="text"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              required
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              placeholder="A brief description of the project"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              GitHub Repository
            </label>
            <input
              type="text"
              value={repositorySlug}
              onChange={(e) => setRepositorySlug(e.target.value)}
              required
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              placeholder="owner/repo"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Project Constitution (YAML)
            </label>
            <textarea
              value={config}
              onChange={(e) => setConfig(e.target.value)}
              required
              rows={8}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm font-mono focus:outline-none focus:ring-2 focus:ring-blue-500"
              placeholder={"project:\n  name: my-app\n  description: ...\n\nstack:\n  frontend: Next.js\n  backend: ASP.NET Core"}
            />
          </div>
          {error && <p className="text-sm text-red-600">{error}</p>}
          <button
            type="submit"
            disabled={creating}
            className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 disabled:opacity-50"
          >
            {creating ? "Creating..." : "Create Project"}
          </button>
        </form>
      )}

      {projects.length === 0 && !showForm ? (
        <div className="text-center py-12 text-gray-500">
          <p>No projects yet. Click &quot;New Project&quot; to create one.</p>
        </div>
      ) : (
        <div className="grid gap-4">
          {projects.map((project) => (
            <Link
              key={project.id}
              href={`/projects/${project.id}`}
              className="block p-4 bg-white rounded-lg border border-gray-200 hover:border-blue-300 transition-colors"
            >
              <h2 className="font-semibold text-gray-900">{project.name}</h2>
              <p className="text-sm text-gray-500 mt-1">{project.description}</p>
              <p className="text-xs text-gray-400 mt-2">{project.repositorySlug}</p>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
