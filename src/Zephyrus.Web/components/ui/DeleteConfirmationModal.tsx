"use client";

import { useEffect, useState } from "react";

interface DeletionPreview {
  entityTitle: string;
  childrenCount: number;
  warnings: string[];
}

interface DeleteConfirmationModalProps {
  entityType: string;
  fetchPreview: () => Promise<DeletionPreview>;
  onConfirm: () => Promise<void>;
  onCancel: () => void;
}

export function DeleteConfirmationModal({
  entityType,
  fetchPreview,
  onConfirm,
  onCancel,
}: DeleteConfirmationModalProps) {
  const [preview, setPreview] = useState<DeletionPreview | null>(null);
  const [loadingPreview, setLoadingPreview] = useState(true);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoadingPreview(true);
    fetchPreview()
      .then(setPreview)
      .catch(() => setError("Failed to load deletion details."))
      .finally(() => setLoadingPreview(false));
    // fetchPreview is intentionally omitted — preview loads once on mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function handleConfirm() {
    setConfirming(true);
    setError(null);
    try {
      await onConfirm();
    } catch {
      setError("Deletion failed. Please try again.");
      setConfirming(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/50"
        onClick={onCancel}
      />

      {/* Modal */}
      <div className="relative bg-white rounded-lg border border-gray-200 shadow-xl p-6 w-full max-w-md mx-4">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Delete {entityType}?
        </h2>

        {loadingPreview ? (
          <div className="flex items-center gap-2 text-sm text-gray-500 py-4">
            <span className="inline-block w-4 h-4 border-2 border-gray-400 border-t-transparent rounded-full animate-spin" />
            Loading details...
          </div>
        ) : preview ? (
          <div className="space-y-3 mb-6">
            <p className="text-sm text-gray-700">
              You are about to permanently delete{" "}
              <span className="font-semibold">
                &ldquo;{preview.entityTitle}&rdquo;
              </span>
              .
            </p>

            {preview.childrenCount > 0 && (
              <p className="text-sm text-gray-600">
                This will also delete{" "}
                <span className="font-semibold">{preview.childrenCount}</span>{" "}
                associated item{preview.childrenCount !== 1 ? "s" : ""}.
              </p>
            )}

            {preview.warnings.length > 0 && (
              <div className="p-3 bg-yellow-50 border border-yellow-200 rounded-md">
                {preview.warnings.map((w, i) => (
                  <p key={i} className="text-sm text-yellow-800">
                    {w}
                  </p>
                ))}
              </div>
            )}

            <p className="text-sm text-red-600 font-medium">
              This action cannot be undone.
            </p>
          </div>
        ) : null}

        {error && (
          <p className="mb-4 text-sm text-red-600">{error}</p>
        )}

        <div className="flex justify-end gap-3">
          <button
            onClick={onCancel}
            disabled={confirming}
            className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            onClick={handleConfirm}
            disabled={loadingPreview || confirming}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-red-600 rounded-md hover:bg-red-700 disabled:opacity-50"
          >
            {confirming ? (
              <>
                <span className="inline-block w-3 h-3 border-2 border-white border-t-transparent rounded-full animate-spin" />
                Deleting...
              </>
            ) : (
              "Delete"
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
