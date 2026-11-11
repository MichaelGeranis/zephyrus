/**
 * The Content-Security-Policy for every page. The UI keeps a team token in
 * the browser, so the policy's main job is to stop injected script from
 * running and reading it:
 *
 * - Scripts run only with this request's nonce ('strict-dynamic' lets those
 *   scripts load Next's chunks). No inline or third-party script runs.
 * - The browser may only call this site and the Zephyrus API.
 * - Images come from this site only, so artifact markdown cannot load
 *   tracking pixels or exfiltrate through image URLs.
 * - Styles allow 'unsafe-inline' because React sets style attributes, which a
 *   nonce cannot cover. Injected CSS cannot run script.
 */
export function contentSecurityPolicy(nonce: string, apiUrl: string, isDev: boolean): string {
  const apiOrigin = new URL(apiUrl).origin;
  return [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${isDev ? " 'unsafe-eval'" : ""}`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' blob: data:",
    "font-src 'self'",
    `connect-src 'self' ${apiOrigin}`,
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
  ].join("; ");
}

/** A fresh, unguessable nonce for one response. */
export function createNonce(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return btoa(String.fromCharCode(...bytes));
}
