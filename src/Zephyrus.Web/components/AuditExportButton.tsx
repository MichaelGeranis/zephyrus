"use client";

import { useState } from "react";

/**
 * Downloads the audit trail: every transition, approval and agent run, with
 * the prompts, models and prompt versions behind each artifact.
 */
export function AuditExportButton({ download }: { download: () => Promise<void> }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleClick() {
    setBusy(true);
    setError(null);
    try {
      await download();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Export failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <span className="flex items-center gap-2">
      <button
        onClick={handleClick}
        disabled={busy}
        className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50"
      >
        {busy ? "Exporting..." : "Export audit trail"}
      </button>
      {error && <span className="text-sm text-red-600">{error}</span>}
    </span>
  );
}
