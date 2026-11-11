"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { ProjectModels as Models, StageModels } from "@/lib/types";

/**
 * The models each pipeline stage of a project uses. Everyone sees them; the
 * server says whether the signed-in member may change them (Tech Leads only).
 */
export function ProjectModels({ projectId }: { projectId: string }) {
  const [models, setModels] = useState<Models | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .getProjectModels(projectId)
      .then(setModels)
      .catch(() => setError("Could not load the models."));
  }, [projectId]);

  if (error && !models) return <p className="text-sm text-red-600">{error}</p>;
  if (!models) return null;

  async function save(stage: string, primary: string, fallback: string | null) {
    setError(null);
    try {
      setModels(await api.chooseProjectModels(projectId, stage, primary, fallback));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save the models.");
    }
  }

  async function reset(stage: string) {
    setError(null);
    try {
      setModels(await api.resetProjectModels(projectId, stage));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not reset the models.");
    }
  }

  return (
    <section aria-labelledby="models-heading" className="mt-8">
      <h2 id="models-heading" className="text-lg font-semibold text-gray-900 mb-1">
        Models
      </h2>
      <p className="text-sm text-gray-500 mb-4">
        {models.canChange
          ? "Which model answers each stage, and which takes over if it fails. Changes apply to the next agent run and are recorded."
          : "Which model answers each stage. Only a Tech Lead can change them."}
      </p>
      <datalist id="known-models">
        {models.knownModels.map((m) => (
          <option key={m} value={m} />
        ))}
      </datalist>
      <div className="bg-white rounded-lg border border-gray-200 divide-y divide-gray-100">
        {models.stages.map((s) => (
          <StageRow key={s.stage} stage={s} canChange={models.canChange} onSave={save} onReset={reset} />
        ))}
      </div>
      {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
    </section>
  );
}

function StageRow({
  stage,
  canChange,
  onSave,
  onReset,
}: {
  stage: StageModels;
  canChange: boolean;
  onSave: (stage: string, primary: string, fallback: string | null) => Promise<void>;
  onReset: (stage: string) => Promise<void>;
}) {
  const [editing, setEditing] = useState(false);
  const [primary, setPrimary] = useState(stage.primary);
  const [fallback, setFallback] = useState(stage.fallback ?? "");

  const source = stage.isProjectChoice
    ? `Chosen by ${stage.changedBy}${stage.changedAt ? ` on ${new Date(stage.changedAt).toLocaleDateString()}` : ""}`
    : "Default";

  return (
    <div className="p-4 flex flex-wrap items-center gap-3">
      <p className="w-24 text-sm font-medium text-gray-900">{stage.stage}</p>
      {editing ? (
        <form
          className="flex flex-1 flex-wrap items-center gap-2"
          onSubmit={async (e) => {
            e.preventDefault();
            await onSave(stage.stage, primary.trim(), fallback.trim() || null);
            setEditing(false);
          }}
        >
          <label className="sr-only" htmlFor={`primary-${stage.stage}`}>
            {stage.stage} primary model
          </label>
          <input
            id={`primary-${stage.stage}`}
            list="known-models"
            value={primary}
            onChange={(e) => setPrimary(e.target.value)}
            className="min-w-0 flex-1 px-2 py-1 border border-gray-300 rounded-md text-sm font-mono"
          />
          <label className="sr-only" htmlFor={`fallback-${stage.stage}`}>
            {stage.stage} fallback model
          </label>
          <input
            id={`fallback-${stage.stage}`}
            list="known-models"
            value={fallback}
            placeholder="default fallback, or none"
            onChange={(e) => setFallback(e.target.value)}
            className="min-w-0 flex-1 px-2 py-1 border border-gray-300 rounded-md text-sm font-mono"
          />
          <button type="submit" className="px-3 py-1 bg-blue-600 text-white text-sm rounded-md hover:bg-blue-700">
            Save
          </button>
          <button type="button" onClick={() => setEditing(false)} className="text-sm text-gray-600">
            Cancel
          </button>
        </form>
      ) : (
        <>
          <p className="min-w-0 flex-1 text-sm font-mono text-gray-700 break-all">
            {stage.primary}
            {stage.fallback && stage.fallback !== "none" ? ` → ${stage.fallback}` : ""}
          </p>
          <p className="text-xs text-gray-500">{source}</p>
          {canChange && (
            <span className="flex gap-3">
              <button onClick={() => setEditing(true)} className="text-sm text-blue-600 hover:text-blue-800">
                Change
              </button>
              {stage.isProjectChoice && (
                <button onClick={() => onReset(stage.stage)} className="text-sm text-gray-600 hover:text-gray-800">
                  Use default
                </button>
              )}
            </span>
          )}
        </>
      )}
    </div>
  );
}
