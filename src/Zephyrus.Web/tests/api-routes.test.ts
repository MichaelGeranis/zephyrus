import { describe, expect, it, vi } from "vitest";
import { api } from "@/lib/api";

// Each client method calls the endpoint the API serves, with the right verb
// and body. A typo here would only show up in a browser.
const routes: [string, () => Promise<unknown>, string, string, unknown?][] = [
  ["getProject", () => api.getProject("p1"), "GET", "/api/projects/p1"],
  ["createProject", () => api.createProject("n", "d", "c", "o/r", "t"), "POST", "/api/projects",
    { name: "n", description: "d", config: "c", repositorySlug: "o/r", gitHubToken: "t" }],
  ["getProjectModels", () => api.getProjectModels("p1"), "GET", "/api/projects/p1/models"],
  ["chooseProjectModels", () => api.chooseProjectModels("p1", "Code", "claude:x", null), "PUT",
    "/api/projects/p1/models/Code", { primary: "claude:x", fallback: null }],
  ["resetProjectModels", () => api.resetProjectModels("p1", "Code"), "DELETE", "/api/projects/p1/models/Code"],
  ["getFeature", () => api.getFeature("f1"), "GET", "/api/features/f1"],
  ["getFeaturesByProject", () => api.getFeaturesByProject("p1"), "GET", "/api/features/by-project/p1"],
  ["createFeature", () => api.createFeature("p1", "idea"), "POST", "/api/features", { projectId: "p1", prompt: "idea" }],
  ["getArtifacts", () => api.getArtifacts("f1"), "GET", "/api/features/f1/artifacts"],
  ["getArtifactContent", () => api.getArtifactContent("f1", "a1"), "GET", "/api/features/f1/artifacts/a1/content"],
  ["getCurrentUser", () => api.getCurrentUser(), "GET", "/api/me"],
  ["updateArtifactContent", () => api.updateArtifactContent("f1", "a1", "# PRD"), "PUT",
    "/api/features/f1/artifacts/a1/content", { content: "# PRD" }],
  ["getTasks", () => api.getTasks("f1"), "GET", "/api/features/f1/tasks"],
  ["getPipelineEvents", () => api.getPipelineEvents("f1"), "GET", "/api/features/f1/pipeline-events"],
  ["getFeatureUsage", () => api.getFeatureUsage("f1"), "GET", "/api/features/f1/usage"],
  ["getAgentJobs", () => api.getAgentJobs("f1"), "GET", "/api/features/f1/agent-jobs"],
  ["getAgentInvocations", () => api.getAgentInvocations("f1"), "GET", "/api/features/f1/agent-invocations"],
  ["getAgentInvocationDetail", () => api.getAgentInvocationDetail("f1", "i1"), "GET",
    "/api/features/f1/agent-invocations/i1"],
  ["retryArtifactCommit", () => api.retryArtifactCommit("f1", "a1"), "POST", "/api/features/f1/artifacts/a1/retry-commit"],
  ["rerunStep", () => api.rerunStep("f1", "Architect"), "POST", "/api/features/f1/rerun-step", { step: "Architect" }],
  ["rerunStep without a step", () => api.rerunStep("f1"), "POST", "/api/features/f1/rerun-step"],
  ["generatePrd", () => api.generatePrd("f1"), "POST", "/api/features/f1/generate-prd"],
  ["getProjectDeletionPreview", () => api.getProjectDeletionPreview("p1"), "GET", "/api/projects/p1/deletion-preview"],
  ["deleteProject", () => api.deleteProject("p1"), "DELETE", "/api/projects/p1"],
  ["getFeatureDeletionPreview", () => api.getFeatureDeletionPreview("f1"), "GET", "/api/features/f1/deletion-preview"],
  ["deleteFeature", () => api.deleteFeature("f1"), "DELETE", "/api/features/f1"],
  ["getArtifactDeletionPreview", () => api.getArtifactDeletionPreview("f1", "a1"), "GET",
    "/api/features/f1/artifacts/a1/deletion-preview"],
  ["deleteArtifact", () => api.deleteArtifact("f1", "a1"), "DELETE", "/api/features/f1/artifacts/a1"],
];

describe("api routes", () => {
  it.each(routes)("%s calls the right endpoint", async (_name, call, method, path, body) => {
    const fetch = vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({}));

    await call();

    const [url, init] = fetch.mock.calls[0];
    expect(url).toBe(`http://localhost:5000${path}`);
    expect(init?.method ?? "GET").toBe(method);
    if (body === undefined) expect(init?.body).toBeUndefined();
    else expect(JSON.parse(init?.body as string)).toEqual(body);
  });
});

describe("audit downloads", () => {
  it.each([
    ["feature", () => api.downloadFeatureAudit("f1"), "/api/features/f1/audit-export", "zephyrus-audit-feature-f1.json"],
    ["project", () => api.downloadProjectAudit("p1"), "/api/projects/p1/audit-export", "zephyrus-audit-project-p1.json"],
  ])("saves the %s audit trail as a named file", async (_scope, call, path, fileName) => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response("{}"));
    URL.createObjectURL = vi.fn(() => "blob:audit");
    URL.revokeObjectURL = vi.fn();
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});

    await call();

    expect(vi.mocked(fetch).mock.calls[0][0]).toBe(`http://localhost:5000${path}`);
    const link = click.mock.contexts[0] as HTMLAnchorElement;
    expect(link.download).toBe(fileName);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:audit");
  });

  it("reports the server's reason when the export is refused", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      Response.json({ detail: "Feature not found." }, { status: 404 }),
    );

    await expect(api.downloadFeatureAudit("missing")).rejects.toMatchObject({
      status: 404,
      message: "Feature not found.",
    });
  });
});
