const TOKEN_KEY = "zephyrus.teamToken";

/**
 * The signed-in team member's bearer token. Held only in this browser tab's
 * session storage: it survives reloads but not closing the browser, so a
 * token left on a shared or lost machine does not stay signed in for good.
 * The backend decides who the caller is from the token, so nothing about the
 * approver identity is chosen here.
 */
export function getTeamToken(): string | null {
  if (typeof window === "undefined") return null;
  try {
    // Earlier versions kept the token in local storage, which never expires:
    // move it into this session once and delete the long-lived copy.
    const legacy = window.localStorage.getItem(TOKEN_KEY);
    if (legacy !== null) {
      window.localStorage.removeItem(TOKEN_KEY);
      if (window.sessionStorage.getItem(TOKEN_KEY) === null) {
        window.sessionStorage.setItem(TOKEN_KEY, legacy);
      }
    }
    return window.sessionStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

export function setTeamToken(token: string): void {
  try {
    window.sessionStorage.setItem(TOKEN_KEY, token);
  } catch {
    /* storage unavailable — the session simply will not persist */
  }
}

export function clearTeamToken(): void {
  try {
    window.sessionStorage.removeItem(TOKEN_KEY);
  } catch {
    /* nothing to do */
  }
}
