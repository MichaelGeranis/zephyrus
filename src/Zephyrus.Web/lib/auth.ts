const TOKEN_KEY = "zephyrus.teamToken";

/**
 * The signed-in team member's bearer token. Held only in this browser —
 * the backend decides who the caller is from the token, so nothing about
 * the approver identity is chosen here.
 */
export function getTeamToken(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

export function setTeamToken(token: string): void {
  try {
    window.localStorage.setItem(TOKEN_KEY, token);
  } catch {
    /* storage unavailable — the session simply will not persist */
  }
}

export function clearTeamToken(): void {
  try {
    window.localStorage.removeItem(TOKEN_KEY);
  } catch {
    /* nothing to do */
  }
}
