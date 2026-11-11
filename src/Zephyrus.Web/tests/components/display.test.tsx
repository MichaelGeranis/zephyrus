import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AgentJobList } from "@/components/AgentJobList";
import { PipelineProgress, StatusBadge } from "@/components/StatusBadge";
import { agentJob } from "../fixtures";

describe("StatusBadge", () => {
  it("shows the status", () => {
    render(<StatusBadge status="PrdPending" />);
    expect(screen.getByText("PrdPending")).toBeInTheDocument();
  });
});

describe("PipelineProgress", () => {
  function dots(container: HTMLElement) {
    return Array.from(container.querySelectorAll<HTMLElement>("[title]"));
  }

  it("marks earlier stages done and the current stage active", () => {
    const { container } = render(<PipelineProgress status="ArchPending" />);
    const stages = dots(container);

    const current = stages.findIndex((d) => d.title === "ArchPending");
    expect(current).toBeGreaterThan(0);
    stages.slice(0, current).forEach((d) => expect(d).toHaveClass("bg-green-500"));
    expect(stages[current]).toHaveClass("bg-blue-500");
    stages.slice(current + 1).forEach((d) => expect(d).toHaveClass("bg-gray-200"));
  });

  it("marks nothing done for an unknown status", () => {
    const { container } = render(<PipelineProgress status="Unknown" />);
    dots(container).forEach((d) => expect(d).toHaveClass("bg-gray-200"));
  });
});

describe("AgentJobList", () => {
  it("renders nothing when there are no jobs", () => {
    const { container } = render(<AgentJobList jobs={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("lists the newest job first", () => {
    render(
      <AgentJobList
        jobs={[
          agentJob({ id: "1", kind: "Architect", status: "Succeeded", attempts: 1 }),
          agentJob({ id: "2", kind: "Task", status: "Running", attempts: 1 }),
        ]}
      />,
    );

    expect(screen.getByRole("region", { name: "Agent Jobs" })).toBeInTheDocument();
    const names = screen.getAllByText(/ Agent$/).map((e) => e.textContent);
    expect(names).toEqual(["Task Agent", "Architect Agent"]);
    expect(screen.getByText("Running")).toBeInTheDocument();
    expect(screen.getByText("Done")).toBeInTheDocument();
  });

  it("shows why a failed job stopped and how to recover", () => {
    render(
      <AgentJobList
        jobs={[agentJob({ status: "Failed", attempts: 2, lastError: "Model returned invalid JSON." })]}
      />,
    );

    expect(screen.getByText("Failed")).toBeInTheDocument();
    expect(screen.getByText(/Model returned invalid JSON\./)).toBeInTheDocument();
    expect(screen.getByText("Re-run Step")).toBeInTheDocument();
    expect(screen.getByText(/Attempt 2 of 2/)).toBeInTheDocument();
  });

  it("shows a queued job with an error as a pending retry", () => {
    render(<AgentJobList jobs={[agentJob({ status: "Queued", attempts: 1, lastError: "Timed out." })]} />);

    expect(screen.getByText("Waiting")).toBeInTheDocument();
    expect(screen.getByText(/Retrying at .* after an error: Timed out\./)).toBeInTheDocument();
  });

  it("counts a job that has not started as its first attempt", () => {
    render(<AgentJobList jobs={[agentJob({ attempts: 0 })]} />);
    expect(screen.getByText(/Attempt 1 of 2/)).toBeInTheDocument();
  });
});
