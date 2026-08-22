"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { api } from "@/lib/api";
import { StatusBadge, PipelineProgress } from "@/components/StatusBadge";
import { DeleteButton } from "@/components/ui/DeleteButton";
import { DeleteConfirmationModal } from "@/components/ui/DeleteConfirmationModal";
import { APPROVABLE_STATUSES, STAGE_LABELS } from "@/lib/types";
import type { Feature, Artifact, TaskItem, PipelineEvent, AgentInvocationSummary } from "@/lib/types";

export default function FeatureDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const [feature, setFeature] = useState<Feature | null>(null);
  const [artifacts, setArtifacts] = useState<Artifact[]>([]);
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [events, setEvents] = useState<PipelineEvent[]>([]);
  const [invocations, setInvocations] = useState<AgentInvocationSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showDeleteModal, setShowDeleteModal] = useState(false);

  const loadData = useCallback(() => {
    Promise.all([
      api.getFeature(id),
      api.getArtifacts(id),
      api.getTasks(id),
      api.getPipelineEvents(id),
      api.getAgentInvocations(id),
    ])
      .then(([f, a, t, e, inv]) => {
        setFeature(f);
        setArtifacts(a);
        setTasks(t);
        setEvents(e);
        setInvocations(inv);
      })
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    loadData();
  }, [loadData]);

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

  async function handleRetryCommit(artifactId: string) {
    setError(null);
    try {
      await api.retryArtifactCommit(id, artifactId);
      loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Retry failed.");
    }
  }

  async function handleRerunStep(step?: string) {
    setGenerating(true);
    setError(null);
    try {
      await api.rerunStep(id, step);
      loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Rerun failed.");
    } finally {
      setGenerating(false);
    }
  }

  const ARTIFACT_STEP: Record<string, string> = {
    Prd: "prd",
    Adr: "architect",
    Task: "tasks",
    Pr: "code",
    Test: "qa",
    Workflow: "devops",
  };

  if (loading) return <p className="text-gray-500">Loading...</p>;
  if (!feature) return <p className="text-red-500">Feature not found.</p>;

  const canGeneratePrd = feature.status === "Ideation";
  const awaitingApproval = feature.status in APPROVABLE_STATUSES;
  const RERUNNABLE_STATUSES = [
    "PrdPending", "ArchPending", "TasksPending", "Coding", "QaPending", "QaApproved",
  ];
  const canRerun = RERUNNABLE_STATUSES.includes(feature.status);

  return (
    <div>
      {showDeleteModal && (
        <DeleteConfirmationModal
          entityType="Feature"
          fetchPreview={() => api.getFeatureDeletionPreview(id)}
          onConfirm={async () => {
            await api.deleteFeature(id);
            router.push(`/projects/${feature.projectId}`);
          }}
          onCancel={() => setShowDeleteModal(false)}
        />
      )}

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
          <div className="flex items-center gap-3">
            <StatusBadge status={feature.status} />
            <DeleteButton onClick={() => setShowDeleteModal(true)} />
          </div>
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

      {canRerun && (
        <div className="mb-6">
          <button
            onClick={() => handleRerunStep()}
            disabled={generating}
            className="px-4 py-2 bg-orange-600 text-white text-sm font-medium rounded-md hover:bg-orange-700 disabled:opacity-50"
          >
            {generating ? "Re-running..." : "Re-run Step"}
          </button>
          {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
        </div>
      )}

      {awaitingApproval && (
        <div className="mb-6 p-3 bg-yellow-50 border border-yellow-200 rounded-lg text-sm text-yellow-800">
          Awaiting approval for <strong>{APPROVABLE_STATUSES[feature.status]}</strong> artifact.
          Review and approve below to advance the pipeline.
        </div>
      )}

      {/* Artifacts */}
      <h2 className="text-lg font-semibold text-gray-900 mb-4">Artifacts</h2>
      {artifacts.length === 0 ? (
        <p className="text-gray-500 text-sm mb-6">No artifacts generated yet.</p>
      ) : (
        <div className="grid gap-3 mb-6">
          {artifacts.map((artifact) => (
            <div
              key={artifact.id}
              className="flex items-center justify-between p-4 bg-white rounded-lg border border-gray-200"
            >
              <div>
                <div className="flex items-center gap-2">
                  <span className="font-medium text-gray-900">{artifact.type}</span>
                  {!artifact.commitSucceeded ? (
                    <span className="text-xs bg-red-100 text-red-700 px-2 py-0.5 rounded-full">
                      Commit failed
                    </span>
                  ) : artifact.approvedAt ? (
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
              <div className="flex items-center gap-3">
                {!artifact.commitSucceeded && (
                  <button
                    onClick={() => handleRetryCommit(artifact.id)}
                    className="text-sm text-red-600 hover:text-red-800 font-medium"
                  >
                    Retry Commit
                  </button>
                )}
                {ARTIFACT_STEP[artifact.type] && (
                  <button
                    onClick={() => handleRerunStep(ARTIFACT_STEP[artifact.type])}
                    disabled={generating}
                    className="text-sm text-orange-600 hover:text-orange-800 font-medium disabled:opacity-50"
                  >
                    Re-run
                  </button>
                )}
                <Link
                  href={`/features/${id}/artifacts/${artifact.id}`}
                  className="text-sm text-blue-600 hover:text-blue-800 font-medium"
                >
                  {artifact.approvedAt ? "View" : "Review & Approve"}
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Tasks */}
      {tasks.length > 0 && (
        <>
          <h2 className="text-lg font-semibold text-gray-900 mb-4">Tasks</h2>
          <div className="grid gap-2 mb-6">
            {tasks.map((task) => (
              <div
                key={task.id}
                className="flex items-center justify-between p-3 bg-white rounded-lg border border-gray-200"
              >
                <div className="flex items-center gap-3">
                  <TaskStatusIcon status={task.status} />
                  <div>
                    <p className="text-sm text-gray-900">{task.title}</p>
                    <div className="flex items-center gap-2 mt-0.5">
                      <span className="text-xs px-1.5 py-0.5 rounded bg-gray-100 text-gray-600 font-mono">
                        {task.agentType}
                      </span>
                      {task.externalIssueId && (
                        <span className="text-xs text-gray-400">
                          Issue #{task.externalIssueId}
                        </span>
                      )}
                      {task.prId && (
                        <span className="text-xs text-gray-400">PR #{task.prId}</span>
                      )}
                    </div>
                  </div>
                </div>
                <span
                  className={`text-xs px-2 py-0.5 rounded-full font-medium ${taskStatusColor(task.status)}`}
                >
                  {task.status}
                </span>
              </div>
            ))}
          </div>
        </>
      )}

      {/* Agent Invocations */}
      {invocations.length > 0 && (
        <>
          <h2 className="text-lg font-semibold text-gray-900 mb-4">Agent Invocations</h2>
          <div className="grid gap-2 mb-6">
            {invocations.map((inv) => (
              <div
                key={inv.id}
                className="flex items-center justify-between p-4 bg-white rounded-lg border border-gray-200"
              >
                <div>
                  <p className="text-sm font-medium text-gray-900 capitalize">
                    {inv.agentName} Agent
                  </p>
                  <p className="text-xs text-gray-400 mt-0.5">
                    {new Date(inv.invokedAt).toLocaleString()} &middot; {inv.durationMs}ms
                  </p>
                </div>
                <Link
                  href={`/features/${id}/invocations/${inv.id}`}
                  className="text-sm text-blue-600 hover:text-blue-800 font-medium"
                >
                  View Prompts
                </Link>
              </div>
            ))}
          </div>
        </>
      )}

      {/* Pipeline Timeline */}
      {events.length > 0 && (
        <>
          <h2 className="text-lg font-semibold text-gray-900 mb-4">Pipeline Timeline</h2>
          <div className="bg-white rounded-lg border border-gray-200 p-4">
            <div className="space-y-0">
              {[...events].reverse().map((event, i) => (
                <div key={event.id} className="flex gap-3">
                  {/* Timeline connector */}
                  <div className="flex flex-col items-center">
                    <div className="w-2.5 h-2.5 rounded-full bg-blue-500 mt-1.5" />
                    {i < events.length - 1 && (
                      <div className="w-px flex-1 bg-gray-200 my-1" />
                    )}
                  </div>
                  {/* Event content */}
                  <div className="pb-4">
                    <p className="text-sm text-gray-900">
                      <span className="text-gray-500">
                        {STAGE_LABELS[event.fromStatus] ?? event.fromStatus}
                      </span>
                      {" → "}
                      <span className="font-medium">
                        {STAGE_LABELS[event.toStatus] ?? event.toStatus}
                      </span>
                    </p>
                    <p className="text-xs text-gray-400 mt-0.5">
                      {event.triggeredBy} &middot;{" "}
                      {new Date(event.timestamp).toLocaleString()}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function TaskStatusIcon({ status }: { status: string }) {
  if (status === "Done")
    return <div className="w-4 h-4 rounded-full bg-green-500 flex-shrink-0" />;
  if (status === "PrOpen")
    return <div className="w-4 h-4 rounded-full bg-blue-500 flex-shrink-0" />;
  if (status === "InProgress")
    return <div className="w-4 h-4 rounded-full bg-yellow-500 flex-shrink-0" />;
  return <div className="w-4 h-4 rounded-full bg-gray-300 flex-shrink-0" />;
}

function taskStatusColor(status: string): string {
  switch (status) {
    case "Done":
      return "bg-green-100 text-green-700";
    case "PrOpen":
      return "bg-blue-100 text-blue-700";
    case "InProgress":
      return "bg-yellow-100 text-yellow-700";
    default:
      return "bg-gray-100 text-gray-600";
  }
}
