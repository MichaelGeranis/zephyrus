import { describe, expect, it, vi } from "vitest";
import { api } from "@/lib/api";
import { clearTeamToken, getTeamToken, setTeamToken } from "@/lib/auth";
import { formatCost, formatTokens } from "@/lib/format";

describe("formatCost", () => {
  it("shows a zero cost as $0", () => {
    expect(formatCost(0)).toBe("$0");
  });

  it("keeps four decimals below a dollar so cheap runs are not shown as $0.00", () => {
    expect(formatCost(0.0042)).toBe("$0.0042");
  });

  it("uses cents from a dollar up", () => {
    expect(formatCost(12.3456)).toBe("$12.35");
  });
});

describe("formatTokens", () => {
  it("abbreviates large counts", () => {
    expect(formatTokens(950)).toBe("950");
    expect(formatTokens(12_400)).toBe("12.4K");
    expect(formatTokens(3_000_000)).toBe("3M");
  });
});

describe("team token storage", () => {
  it("stores, reads and clears the token", () => {
    expect(getTeamToken()).toBeNull();
    setTeamToken("secret");
    expect(getTeamToken()).toBe("secret");
    clearTeamToken();
    expect(getTeamToken()).toBeNull();
  });

  it("keeps the token for this session only, never in local storage", () => {
    setTeamToken("secret");
    expect(window.sessionStorage.getItem("zephyrus.teamToken")).toBe("secret");
    expect(window.localStorage.getItem("zephyrus.teamToken")).toBeNull();
  });

  it("moves a token left in local storage by an earlier version into the session", () => {
    window.localStorage.setItem("zephyrus.teamToken", "legacy");

    expect(getTeamToken()).toBe("legacy");
    expect(window.localStorage.getItem("zephyrus.teamToken")).toBeNull();
    expect(window.sessionStorage.getItem("zephyrus.teamToken")).toBe("legacy");
  });

  it("returns null when storage is unavailable", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    expect(getTeamToken()).toBeNull();
  });
});

describe("api", () => {
  function mockFetch(response: Response) {
    return vi.spyOn(globalThis, "fetch").mockResolvedValue(response);
  }

  it("sends the stored team token as a bearer token", async () => {
    setTeamToken("demo-pm");
    const fetch = mockFetch(Response.json([]));

    await api.getProjects();

    const [url, init] = fetch.mock.calls[0];
    expect(url).toBe("http://localhost:5000/api/projects");
    expect(new Headers(init?.headers).get("Authorization")).toBe("Bearer demo-pm");
  });

  it("sends no Authorization header when signed out", async () => {
    const fetch = mockFetch(Response.json([]));

    await api.getProjects();

    expect(new Headers(fetch.mock.calls[0][1]?.headers).has("Authorization")).toBe(false);
  });

  it("approves without sending an approver identity", async () => {
    const fetch = mockFetch(Response.json({}));

    await api.approveArtifact("feature-1", "artifact-1");

    const [url, init] = fetch.mock.calls[0];
    expect(url).toBe("http://localhost:5000/api/features/feature-1/artifacts/artifact-1/approve");
    expect(init?.method).toBe("POST");
    expect(init?.body).toBeUndefined();
  });

  it("shows the detail of a problem response", async () => {
    mockFetch(Response.json({ status: 400, detail: "Feature must be in PrdPending status." }, { status: 400 }));

    await expect(api.getProjects()).rejects.toThrow(/^Feature must be in PrdPending status\.$/);
  });

  it("adds the trace ID to an unexpected error so it can be found in the logs", async () => {
    mockFetch(Response.json({ status: 500, detail: "An unexpected error occurred.", traceId: "abc123" }, { status: 500 }));

    await expect(api.getProjects()).rejects.toThrow("An unexpected error occurred. (trace ID: abc123)");
  });

  it("carries the status code on the error", async () => {
    mockFetch(Response.json({ detail: "Too many failed sign-ins. Try again later." }, { status: 429 }));

    await expect(api.getProjects()).rejects.toMatchObject({ status: 429, message: "Too many failed sign-ins. Try again later." });
  });

  it("throws the server's error text on a failed request", async () => {
    mockFetch(new Response("Only a PM/EM may approve a PRD.", { status: 403 }));

    await expect(api.approveArtifact("f", "a")).rejects.toThrow("Only a PM/EM may approve a PRD.");
  });

  it("falls back to the status code when the error has no body", async () => {
    mockFetch(new Response(null, { status: 500 }));

    await expect(api.getProjects()).rejects.toThrow("Request failed: 500");
  });
});

describe("audit export download", () => {
  it("fetches the export with the team token and saves it under the export's name", async () => {
    setTeamToken("demo-qa");
    const fetch = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response('{"format":"zephyrus.audit/v1"}'));
    const createObjectURL = vi.fn(() => "blob:export");
    const revokeObjectURL = vi.fn();
    vi.stubGlobal("URL", Object.assign(URL, { createObjectURL, revokeObjectURL }));
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) {
      expect(this.download).toBe("zephyrus-audit-feature-f1.json");
      expect(this.href).toBe("blob:export");
    });

    await api.downloadFeatureAudit("f1");

    const [url, init] = fetch.mock.calls[0];
    expect(url).toBe("http://localhost:5000/api/features/f1/audit-export");
    expect(new Headers(init?.headers).get("Authorization")).toBe("Bearer demo-qa");
    expect(click).toHaveBeenCalledOnce();
    expect(revokeObjectURL).toHaveBeenCalledWith("blob:export");
  });

  it("reports a refused export instead of saving it", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({ detail: "Project 'p1' not found." }, { status: 404 }));

    await expect(api.downloadProjectAudit("p1")).rejects.toThrow("Project 'p1' not found.");
  });
});
