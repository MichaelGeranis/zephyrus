"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { api } from "@/lib/api";
import { ApprovalGate } from "@/components/ApprovalGate";
import type { Artifact } from "@/lib/types";

export default function ArtifactApprovalPage() {
  const { id: featureId, artifactId } = useParams<{ id: string; artifactId: string }>();
  const router = useRouter();
  const [artifact, setArtifact] = useState<Artifact | null>(null);
  const [content, setContent] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      api.getArtifacts(featureId).then((artifacts) =>
        artifacts.find((a) => a.id === artifactId) ?? null
      ),
      api.getArtifactContent(featureId, artifactId).then((res) => res.content).catch(() => null),
    ])
      .then(([a, c]) => {
        setArtifact(a);
        setContent(c);
        if (!a) setError("Artifact not found.");
      })
      .finally(() => setLoading(false));
  }, [featureId, artifactId]);

  function handleApproved(updated: Artifact) {
    setArtifact(updated);
    // Refresh feature to pick up new status after a brief delay
    setTimeout(() => router.push(`/features/${featureId}`), 1500);
  }

  if (loading) return <p className="text-gray-500">Loading artifact...</p>;
  if (error || !artifact) return <p className="text-red-500">{error ?? "Not found."}</p>;

  return (
    <div>
      <div className="mb-6">
        <Link
          href={`/features/${featureId}`}
          className="text-sm text-blue-600 hover:text-blue-800"
        >
          &larr; Back to feature
        </Link>
      </div>

      <ApprovalGate
        featureId={featureId}
        artifact={artifact}
        content={content ?? "*Content not available — file may not exist in the repository yet.*"}
        onApproved={handleApproved}
      />
    </div>
  );
}
