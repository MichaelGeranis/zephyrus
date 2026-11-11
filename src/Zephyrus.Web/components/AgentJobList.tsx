import type { AgentJob, AgentJobStatus } from "@/lib/types";

const STATUS_STYLE: Record<AgentJobStatus, { label: string; className: string }> = {
  Queued: { label: "Waiting", className: "bg-gray-100 text-gray-700" },
  Running: { label: "Running", className: "bg-blue-100 text-blue-800" },
  Succeeded: { label: "Done", className: "bg-green-100 text-green-800" },
  Failed: { label: "Failed", className: "bg-red-100 text-red-800" },
};

/**
 * Background agent jobs for a feature, newest first. Failed jobs show their
 * error so the team knows why the pipeline stopped.
 */
export function AgentJobList({ jobs }: { jobs: AgentJob[] }) {
  if (jobs.length === 0) return null;

  const ordered = [...jobs].reverse();

  return (
    <section aria-label="Agent Jobs">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">Agent Jobs</h2>
      <div className="grid gap-2 mb-6">
        {ordered.map((job) => {
          const style = STATUS_STYLE[job.status];
          const retrying = job.status === "Queued" && job.lastError;
          return (
            <div key={job.id} className="p-4 bg-white rounded-lg border border-gray-200 min-w-0">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-gray-900">{job.kind} Agent</p>
                  <span className={`text-xs px-2 py-0.5 rounded-full ${style.className}`}>{style.label}</span>
                </div>
                <p className="text-xs text-gray-500 tabular-nums">
                  Attempt {Math.max(job.attempts, 1)} of {job.maxAttempts} &middot;{" "}
                  {new Date(job.completedAt ?? job.startedAt ?? job.enqueuedAt).toLocaleString()}
                </p>
              </div>
              {retrying && (
                <p className="mt-2 text-xs text-amber-800">
                  Retrying at {new Date(job.nextAttemptAt).toLocaleTimeString()} after an error: {job.lastError}
                </p>
              )}
              {job.status === "Failed" && (
                <p className="mt-2 text-xs text-red-700 break-words">
                  {job.lastError} Use <span className="font-medium">Re-run Step</span> to try again.
                </p>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
}
