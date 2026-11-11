import { describe, expect, it } from "vitest";
import { NextRequest } from "next/server";
import { contentSecurityPolicy, createNonce } from "@/lib/csp";
import { proxy } from "@/proxy";

function directive(policy: string, name: string) {
  return policy.split("; ").find((d) => d.startsWith(`${name} `));
}

describe("contentSecurityPolicy", () => {
  it("allows scripts only with the request's nonce", () => {
    const policy = contentSecurityPolicy("abc123", "http://localhost:5000", false);
    expect(directive(policy, "script-src")).toBe("script-src 'self' 'nonce-abc123' 'strict-dynamic'");
  });

  it("lets the browser call the API's origin and nothing else", () => {
    const policy = contentSecurityPolicy("n", "https://api.example.com/base/", false);
    expect(directive(policy, "connect-src")).toBe("connect-src 'self' https://api.example.com");
  });

  it("allows eval only in development, where React needs it", () => {
    expect(contentSecurityPolicy("n", "http://localhost:5000", true)).toContain("'unsafe-eval'");
    expect(contentSecurityPolicy("n", "http://localhost:5000", false)).not.toContain("'unsafe-eval'");
  });

  it("forbids framing, plugins and foreign images", () => {
    const policy = contentSecurityPolicy("n", "http://localhost:5000", false);
    expect(policy).toContain("frame-ancestors 'none'");
    expect(policy).toContain("object-src 'none'");
    expect(directive(policy, "img-src")).toBe("img-src 'self' blob: data:");
  });
});

describe("createNonce", () => {
  it("is different for every response", () => {
    const nonces = new Set(Array.from({ length: 20 }, createNonce));
    expect(nonces.size).toBe(20);
    expect(createNonce()).toMatch(/^[A-Za-z0-9+/]{22}==$/);
  });
});

describe("proxy", () => {
  it("sends the policy to the browser and to the renderer with the same nonce", () => {
    const response = proxy(new NextRequest("http://localhost:3000/projects"));
    const policy = response.headers.get("Content-Security-Policy");
    expect(policy).toMatch(/'nonce-[^']+'/);
    // Next passes rewritten request headers to the renderer this way.
    expect(response.headers.get("x-middleware-request-content-security-policy")).toBe(policy);
  });
});
