"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import type { Project } from "@/lib/types";

export default function ProjectsPage() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.getProjects().then(setProjects).finally(() => setLoading(false));
  }, []);

  if (loading) return <p className="text-gray-500">Loading projects...</p>;

  if (projects.length === 0) {
    return (
      <div className="text-center py-12 text-gray-500">
        <p>No projects yet. Create one via the API.</p>
      </div>
    );
  }

  return (
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Projects</h1>
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
    </div>
  );
}
