import { describe, expect, it } from "vitest";
import { loadFeatureData, type FeatureDataSource } from "@/lib/featureData";
import type { AgentJob, Artifact } from "@/lib/types";

/**
 * A backend whose PRD Agent finishes at the worst moment for the page: while
 * the page is reading the agent jobs. As in the real worker, the agent saves
 * its artifact first and its job is marked succeeded afterwards.
 */
function backendWhereTheAgentFinishesDuringTheJobsRead() {
  const prd = { id: "prd-1", type: "Prd" } as Artifact;
  const job = { id: "job-1", kind: "Prd", status: "Running" } as AgentJob;
  const artifacts: Artifact[] = [];

  function agentFinishes() {
    artifacts.push(prd);
    job.status = "Succeeded";
  }

  const source: FeatureDataSource = {
    getFeature: async () => ({ id: "f-1", status: "PrdPending" }) as never,
    getArtifacts: async () => [...artifacts],
    getTasks: async () => [],
    getPipelineEvents: async () => [],
    getAgentInvocations: async () => [],
    getAgentJobs: async () => {
      agentFinishes();
      return [{ ...job }];
    },
    getFeatureUsage: async () => ({}) as never,
  };
  return source;
}

describe("loadFeatureData", () => {
  it("never pairs a finished agent job with the artifacts from before it finished", async () => {
    const data = await loadFeatureData("f-1", backendWhereTheAgentFinishesDuringTheJobsRead());

    expect(data.agentJobs.map((j) => j.status)).toEqual(["Succeeded"]);
    // The page stops polling once no job is queued or running, so the PRD
    // must already be in this load, or it never appears.
    expect(data.artifacts.map((a) => a.id)).toEqual(["prd-1"]);
  });

  it("reads the agent jobs before anything else", async () => {
    const calls: string[] = [];
    const record =
      <T,>(name: string, value: T) =>
      async () => {
        calls.push(name);
        return value;
      };
    const source = {
      getFeature: record("feature", {}),
      getArtifacts: record("artifacts", []),
      getTasks: record("tasks", []),
      getPipelineEvents: record("events", []),
      getAgentInvocations: record("invocations", []),
      getAgentJobs: record("jobs", []),
      getFeatureUsage: record("usage", {}),
    } as unknown as FeatureDataSource;

    await loadFeatureData("f-1", source);

    expect(calls[0]).toBe("jobs");
    expect(calls).toHaveLength(7);
  });
});
