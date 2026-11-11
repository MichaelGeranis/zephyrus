"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { StatusBadge, PipelineProgress } from "@/components/StatusBadge";
import { PIPELINE_STAGES, STAGE_LABELS } from "@/lib/types";
import type { Project, Feature } from "@/lib/types";

interface ProjectWithFeatures {
  project: Project;
  features: Feature[];
}

export default function DashboardPage() {
  const [data, setData] = useState<ProjectWithFeatures[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .getProjects()
      .then(async (projects) => {
        const results = await Promise.all(
          projects.map(async (project) => ({
            project,
            features: await api.getFeaturesByProject(project.id),
          }))
        );
        setData(results);
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <p className="text-gray-500">Loading dashboard...</p>;

  // Aggregate stats
  const allFeatures = data.flatMap((d) => d.features);
  const statusCounts = PIPELINE_STAGES.reduce(
    (acc, stage) => {
      acc[stage] = allFeatures.filter((f) => f.status === stage).length;
      return acc;
    },
    {} as Record<string, number>
  );
  const totalFeatures = allFeatures.length;
  const deployed = statusCounts["Deployed"] ?? 0;
  const inProgress = totalFeatures - deployed - (statusCounts["Ideation"] ?? 0);

  return (
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Pipeline Dashboard</h1>

      {/* Summary cards */}
      <div className="grid grid-cols-4 gap-4 mb-8">
        <SummaryCard label="Total Features" value={totalFeatures} />
        <SummaryCard label="In Progress" value={inProgress} color="text-blue-600" />
        <SummaryCard label="Deployed" value={deployed} color="text-emerald-600" />
        <SummaryCard label="Projects" value={data.length} color="text-gray-600" />
      </div>

      {/* Pipeline distribution */}
      <div className="bg-white rounded-lg border border-gray-200 p-6 mb-8">
        <h2 className="text-sm font-semibold text-gray-700 mb-4">Pipeline Distribution</h2>
        <div className="flex items-end gap-2 h-32">
          {PIPELINE_STAGES.map((stage) => {
            const count = statusCounts[stage] ?? 0;
            const height = totalFeatures > 0 ? (count / totalFeatures) * 100 : 0;
            return (
              <div key={stage} className="flex-1 flex flex-col items-center gap-1">
                <span className="text-xs font-medium text-gray-700">{count}</span>
                <div
                  className={`w-full rounded-t ${count > 0 ? stageBarColor(stage) : "bg-gray-100"}`}
                  style={{ height: `${Math.max(height, count > 0 ? 8 : 2)}%` }}
                />
                <span className="text-[10px] text-gray-500 text-center leading-tight">
                  {STAGE_LABELS[stage]?.split(" ")[0]}
                </span>
              </div>
            );
          })}
        </div>
      </div>

      {/* Per-project feature lists */}
      {data.length === 0 ? (
        <p className="text-gray-500">No projects yet. Create one via the API.</p>
      ) : (
        <div className="space-y-8">
          {data.map(({ project, features }) => (
            <div key={project.id} className="bg-white rounded-lg border border-gray-200 p-6">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <Link
                    href={`/projects/${project.id}`}
                    className="text-lg font-semibold text-gray-900 hover:text-blue-600"
                  >
                    {project.name}
                  </Link>
                  <p className="text-sm text-gray-500">{project.repositorySlug}</p>
                </div>
                <span className="text-sm text-gray-400">
                  {features.length} feature{features.length !== 1 ? "s" : ""}
                </span>
              </div>

              {features.length === 0 ? (
                <p className="text-sm text-gray-400">No features yet.</p>
              ) : (
                <div className="space-y-3">
                  {features.map((feature) => (
                    <Link
                      key={feature.id}
                      href={`/features/${feature.id}`}
                      className="flex items-center justify-between p-3 rounded-md border border-gray-100 hover:border-blue-200 hover:bg-blue-50/30 transition-colors"
                    >
                      <div className="flex-1 min-w-0 mr-4">
                        <p className="text-sm text-gray-900 truncate">{feature.prompt}</p>
                        <div className="mt-1.5">
                          <PipelineProgress status={feature.status} />
                        </div>
                      </div>
                      <StatusBadge status={feature.status} />
                    </Link>
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function SummaryCard({
  label,
  value,
  color = "text-gray-900",
}: {
  label: string;
  value: number;
  color?: string;
}) {
  return (
    <div className="bg-white rounded-lg border border-gray-200 p-4">
      <p className="text-sm text-gray-500">{label}</p>
      <p className={`text-2xl font-bold mt-1 ${color}`}>{value}</p>
    </div>
  );
}

function stageBarColor(stage: string): string {
  if (stage === "Deployed") return "bg-emerald-400";
  if (stage.endsWith("Approved")) return "bg-green-400";
  if (stage.endsWith("Pending")) return "bg-yellow-400";
  if (stage === "Coding") return "bg-blue-400";
  return "bg-gray-300";
}
