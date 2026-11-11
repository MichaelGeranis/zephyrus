"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { CurrentUser } from "@/lib/types";
import { getTeamToken, setTeamToken, clearTeamToken } from "@/lib/auth";

/**
 * Header sign-in. Every API call requires a team token, so pages cannot load
 * their data until one is set. Signing in or out reloads the page so each
 * page re-fetches with the new credentials.
 */
export function SessionBar() {
  const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null);
  const [checked, setChecked] = useState(false);
  const [tokenInput, setTokenInput] = useState("");
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const hasToken = getTeamToken() !== null;
    (hasToken ? api.getCurrentUser() : Promise.resolve(null))
      .then(setCurrentUser)
      .catch(() => clearTeamToken())
      .finally(() => setChecked(true));
  }, []);

  async function handleSignIn(e: React.FormEvent) {
    e.preventDefault();
    if (!tokenInput.trim()) {
      setError("Token required.");
      return;
    }

    setIsSigningIn(true);
    setError(null);
    setTeamToken(tokenInput.trim());

    try {
      await api.getCurrentUser();
      window.location.reload();
    } catch (err) {
      clearTeamToken();
      // 429: too many wrong tokens from this address; the server says when to retry.
      setError(err instanceof ApiError && err.status === 429 ? err.message : "Token not recognised.");
      setIsSigningIn(false);
    }
  }

  function handleSignOut() {
    clearTeamToken();
    window.location.reload();
  }

  if (!checked) return null;

  if (currentUser) {
    return (
      <div className="ml-auto flex items-center gap-3 text-sm text-gray-600">
        <span>
          Signed in as <span className="font-medium text-gray-900">{currentUser.displayName}</span>
        </span>
        <button onClick={handleSignOut} className="text-blue-600 hover:text-blue-800">
          Sign out
        </button>
      </div>
    );
  }

  return (
    <form onSubmit={handleSignIn} className="ml-auto flex items-center gap-2">
      <label htmlFor="headerTeamToken" className="sr-only">
        Team token
      </label>
      <input
        id="headerTeamToken"
        type="password"
        autoComplete="off"
        value={tokenInput}
        onChange={(e) => setTokenInput(e.target.value)}
        placeholder="Team token"
        className="w-40 px-2 py-1 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
      />
      <button
        type="submit"
        disabled={isSigningIn}
        className="px-3 py-1 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 disabled:opacity-50"
      >
        {isSigningIn ? "Signing in..." : "Sign in"}
      </button>
      {error && <span className="text-sm text-red-600">{error}</span>}
    </form>
  );
}
