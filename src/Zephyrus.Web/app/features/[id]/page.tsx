"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { api } from "@/lib/api";
import { StatusBadge, PipelineProgress } from "@/components/StatusBadge";
import { APPROVABLE_STATUSES } from "@/lib/types";
import type { Feature, Artifact } from "@/lib/types";

export default function FeatureDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [feature, setFeature] = useState<Feature | null>(null);
  const [artifacts, setArtifacts] = useState<Artifact[]>([]);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function loadData() {
    Promise.all([api.getFeature(id), api.getArtifacts(id)])
      .then(([f, a]) => {
        setFeature(f);
        setArtifacts(a);
      })
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    loadData();
  }, [id]);

  async function handleGeneratePrd() {
    setGenerating(true);
    setError(null);
    try {
      await api.generatePrd(id);
      loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to generate PRD.");
    } finally {
      setGenerating(false);
    }
  }

  if (loading) return <p className="text-gray-500">Loading...</p>;
  if (!feature) return <p className="text-red-500">Feature not found.</p>;

  const canGeneratePrd = feature.status === "Ideation";
  const awaitingApproval = feature.status in APPROVABLE_STATUSES;

  return (
    <div>
      <div className="mb-6">
        <Link
          href={`/projects/${feature.projectId}`}
          className="text-sm text-blue-600 hover:text-blue-800"
        >
          &larr; Back to project
        </Link>
      </div>

      {/* Feature header */}
      <div className="bg-white rounded-lg border border-gray-200 p-6 mb-6">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-bold text-gray-900">{feature.prompt}</h1>
            <p className="text-xs text-gray-400 mt-1">ID: {feature.id}</p>
          </div>
          <StatusBadge status={feature.status} />
        </div>
        <div className="mt-4">
          <PipelineProgress status={feature.status} />
        </div>
      </div>

      {/* Actions */}
      {canGeneratePrd && (
        <div className="mb-6">
          <button
            onClick={handleGeneratePrd}
            disabled={generating}
            className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 disabled:opacity-50"
          >
            {generating ? "Generating PRD..." : "Generate PRD"}
          </button>
          {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
        </div>
      )}

      {/* Artifacts */}
      <h2 className="text-lg font-semibold text-gray-900 mb-4">Artifacts</h2>
      {artifacts.length === 0 ? (
        <p className="text-gray-500 text-sm">No artifacts generated yet.</p>
      ) : (
        <div className="grid gap-3">
          {artifacts.map((artifact) => (
            <div
              key={artifact.id}
              className="flex items-center justify-between p-4 bg-white rounded-lg border border-gray-200"
            >
              <div>
                <div className="flex items-center gap-2">
                  <span className="font-medium text-gray-900">{artifact.type}</span>
                  {artifact.approvedAt ? (
                    <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded-full">
                      Approved
                    </span>
                  ) : (
                    <span className="text-xs bg-yellow-100 text-yellow-700 px-2 py-0.5 rounded-full">
                      Pending review
                    </span>
                  )}
                </div>
                <p className="text-xs text-gray-400 mt-1">{artifact.repositoryPath}</p>
              </div>
              <Link
                href={`/features/${id}/artifacts/${artifact.id}`}
                className="text-sm text-blue-600 hover:text-blue-800 font-medium"
              >
                {artifact.approvedAt ? "View" : "Review & Approve"}
              </Link>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
