import { NextResponse, type NextRequest } from "next/server";
import { contentSecurityPolicy, createNonce } from "@/lib/csp";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

/**
 * Gives every page a fresh script nonce and the matching Content-Security-Policy.
 * Next.js reads the policy from the request headers and puts the nonce on
 * its own scripts, which is why pages are rendered per request (see the
 * root layout).
 */
export function proxy(request: NextRequest) {
  const policy = contentSecurityPolicy(createNonce(), API_URL, process.env.NODE_ENV === "development");

  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("Content-Security-Policy", policy);

  const response = NextResponse.next({ request: { headers: requestHeaders } });
  response.headers.set("Content-Security-Policy", policy);
  return response;
}

export const config = {
  matcher: [
    {
      // Pages only: static files and prefetches need no policy.
      source: "/((?!_next/static|_next/image|favicon.ico).*)",
      missing: [
        { type: "header", key: "next-router-prefetch" },
        { type: "header", key: "purpose", value: "prefetch" },
      ],
    },
  ],
};
