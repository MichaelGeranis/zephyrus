"use client";

import { useEffect, useState } from "react";
import ReactMarkdown from "react-markdown";
import { api } from "@/lib/api";
import type { Artifact, CurrentUser } from "@/lib/types";
import { getTeamToken, setTeamToken, clearTeamToken } from "@/lib/auth";

interface ApprovalGateProps {
  featureId: string;
  artifact: Artifact;
  content: string;
  onApproved: (artifact: Artifact) => void;
}

export function ApprovalGate({ featureId, artifact, content, onApproved }: ApprovalGateProps) {
  const [editedContent, setEditedContent] = useState(content);
  const [isEditing, setIsEditing] = useState(false);
  const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null);
  const [tokenInput, setTokenInput] = useState("");
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);

  useEffect(() => {
    if (!getTeamToken()) return;
    api.getCurrentUser().then(setCurrentUser).catch(() => clearTeamToken());
  }, []);

  async function handleSignIn() {
    if (!tokenInput.trim()) {
      setError("A team token is required.");
      return;
    }

    setIsSigningIn(true);
    setError(null);
    setTeamToken(tokenInput.trim());

    try {
      setCurrentUser(await api.getCurrentUser());
      setTokenInput("");
    } catch {
      clearTeamToken();
      setError("That token was not recognised.");
    } finally {
      setIsSigningIn(false);
    }
  }

  const isAlreadyApproved = artifact.approvedAt !== null;
  const hasChanges = editedContent !== content;

  async function handleSave() {
    setIsSaving(true);
    setError(null);
    setSaveSuccess(false);

    try {
      await api.updateArtifactContent(featureId, artifact.id, editedContent);
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 3000);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Save failed.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleApprove() {
    setIsSubmitting(true);
    setError(null);

    try {
      const updated = await api.approveArtifact(featureId, artifact.id);
      onApproved(updated);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Approval failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="space-y-6">
      {/* Artifact header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold text-gray-900">
            {artifact.type} Artifact
          </h2>
          <p className="text-sm text-gray-500">{artifact.repositoryPath}</p>
        </div>
        {isAlreadyApproved ? (
          <div className="text-sm text-green-700 bg-green-50 px-3 py-1.5 rounded-md">
            Approved by {artifact.approvedBy} on{" "}
            {new Date(artifact.approvedAt!).toLocaleDateString()}
          </div>
        ) : (
          <div className="flex items-center gap-3">
            {isEditing && hasChanges && (
              <button
                onClick={handleSave}
                disabled={isSaving}
                className="text-sm px-3 py-1 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50"
              >
                {isSaving ? "Saving..." : "Save"}
              </button>
            )}
            {saveSuccess && (
              <span className="text-sm text-green-600">Saved</span>
            )}
            <button
              onClick={() => setIsEditing(!isEditing)}
              className="text-sm text-blue-600 hover:text-blue-800"
            >
              {isEditing ? "Preview" : "Edit"}
            </button>
          </div>
        )}
      </div>

      {/* Content display / editor */}
      <div className="border border-gray-200 rounded-lg overflow-hidden">
        {isEditing ? (
          <textarea
            value={editedContent}
            onChange={(e) => setEditedContent(e.target.value)}
            className="w-full h-[500px] p-4 font-mono text-sm focus:outline-none resize-none"
          />
        ) : (
          <div className="p-6 prose prose-sm max-w-none bg-white">
            <ReactMarkdown>{editedContent}</ReactMarkdown>
          </div>
        )}
      </div>

      {/* Approval form */}
      {!isAlreadyApproved && (
        <div className="border border-gray-200 rounded-lg p-4 bg-gray-50">
          <h3 className="text-sm font-medium text-gray-700 mb-3">Approve this artifact</h3>
          {currentUser ? (
            <div className="flex items-end justify-between gap-3">
              <div className="text-sm text-gray-600">
                Approving as{" "}
                <span className="font-medium text-gray-900">{currentUser.displayName}</span>{" "}
                <span className="text-gray-500">({currentUser.roles.join(", ") || "no roles"})</span>
              </div>
              <button
                onClick={handleApprove}
                disabled={isSubmitting}
                className="px-4 py-2 bg-green-600 text-white text-sm font-medium rounded-md hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isSubmitting ? "Approving..." : "Approve"}
              </button>
            </div>
          ) : (
            <div className="flex items-end gap-3">
              <div className="flex-1">
                <label htmlFor="teamToken" className="block text-sm text-gray-600 mb-1">
                  Team token
                </label>
                <input
                  id="teamToken"
                  type="password"
                  value={tokenInput}
                  onChange={(e) => setTokenInput(e.target.value)}
                  placeholder="Sign in to approve"
                  className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                />
              </div>
              <button
                onClick={handleSignIn}
                disabled={isSigningIn}
                className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isSigningIn ? "Signing in..." : "Sign in"}
              </button>
            </div>
          )}
          {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
        </div>
      )}
    </div>
  );
}
