"use client";

import { PIPELINE_STAGES } from "@/lib/types";

const STATUS_COLORS: Record<string, string> = {
  Ideation: "bg-gray-100 text-gray-700",
  PrdPending: "bg-yellow-100 text-yellow-800",
  PrdApproved: "bg-green-100 text-green-800",
  ArchPending: "bg-yellow-100 text-yellow-800",
  ArchApproved: "bg-green-100 text-green-800",
  TasksPending: "bg-yellow-100 text-yellow-800",
  TasksApproved: "bg-green-100 text-green-800",
  Coding: "bg-blue-100 text-blue-800",
  QaPending: "bg-yellow-100 text-yellow-800",
  QaApproved: "bg-green-100 text-green-800",
  Deployed: "bg-emerald-100 text-emerald-800",
};

export function StatusBadge({ status }: { status: string }) {
  const color = STATUS_COLORS[status] ?? "bg-gray-100 text-gray-700";
  return (
    <span className={`inline-block px-2.5 py-0.5 rounded-full text-xs font-medium ${color}`}>
      {status}
    </span>
  );
}

export function PipelineProgress({ status }: { status: string }) {
  const currentIndex = PIPELINE_STAGES.indexOf(status as (typeof PIPELINE_STAGES)[number]);

  return (
    <div className="flex items-center gap-1">
      {PIPELINE_STAGES.map((stage, i) => (
        <div key={stage} className="flex items-center">
          <div
            className={`w-2.5 h-2.5 rounded-full ${
              i < currentIndex
                ? "bg-green-500"
                : i === currentIndex
                  ? "bg-blue-500"
                  : "bg-gray-200"
            }`}
            title={stage}
          />
          {i < PIPELINE_STAGES.length - 1 && (
            <div className={`w-4 h-0.5 ${i < currentIndex ? "bg-green-500" : "bg-gray-200"}`} />
          )}
        </div>
      ))}
    </div>
  );
}
