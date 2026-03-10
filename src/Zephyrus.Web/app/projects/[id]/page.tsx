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

  useEffect(() => {
    Promise.all([api.getProject(id), api.getFeaturesByProject(id)])
      .then(([p, f]) => {
        setProject(p);
        setFeatures(f);
      })
      .finally(() => setLoading(false));
  }, [id]);

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

      <h2 className="text-lg font-semibold text-gray-900 mb-4">Features</h2>

      {features.length === 0 ? (
        <p className="text-gray-500">No features yet. Create one via the API.</p>
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
