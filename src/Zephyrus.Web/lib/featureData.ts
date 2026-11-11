import { api } from "./api";
import type {
  AgentInvocationSummary,
  AgentJob,
  Artifact,
  Feature,
  FeatureUsage,
  PipelineEvent,
  TaskItem,
} from "./types";

export interface FeatureData {
  feature: Feature;
  artifacts: Artifact[];
  tasks: TaskItem[];
  events: PipelineEvent[];
  invocations: AgentInvocationSummary[];
  agentJobs: AgentJob[];
  usage: FeatureUsage;
}

export type FeatureDataSource = Pick<
  typeof api,
  | "getFeature"
  | "getArtifacts"
  | "getTasks"
  | "getPipelineEvents"
  | "getAgentInvocations"
  | "getAgentJobs"
  | "getFeatureUsage"
>;

/**
 * Loads everything the feature page shows.
 *
 * The agent jobs are read first and the rest only once they have arrived.
 * An agent saves its output before its job is marked succeeded, so a job
 * reported as finished always comes with the artifacts it wrote. Reading
 * everything in parallel could pair a finished job with artifacts read just
 * before the agent saved them; the page then stops polling, because no job
 * is running, and never shows the result.
 */
export async function loadFeatureData(id: string, source: FeatureDataSource = api): Promise<FeatureData> {
  const agentJobs = await source.getAgentJobs(id);
  const [feature, artifacts, tasks, events, invocations, usage] = await Promise.all([
    source.getFeature(id),
    source.getArtifacts(id),
    source.getTasks(id),
    source.getPipelineEvents(id),
    source.getAgentInvocations(id),
    source.getFeatureUsage(id),
  ]);
  return { feature, artifacts, tasks, events, invocations, agentJobs, usage };
}
