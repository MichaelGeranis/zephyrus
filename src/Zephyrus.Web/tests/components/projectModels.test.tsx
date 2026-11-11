import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ProjectModels } from "@/components/ProjectModels";
import { api } from "@/lib/api";
import type { ProjectModels as Models } from "@/lib/types";

vi.mock("@/lib/api", () => ({
  api: { getProjectModels: vi.fn(), chooseProjectModels: vi.fn(), resetProjectModels: vi.fn() },
}));

const mocked = vi.mocked(api);

function models(overrides: Partial<Models> = {}): Models {
  return {
    canChange: true,
    knownModels: ["claude:claude-opus-5-5", "local:llama3.1:8b"],
    stages: [
      { stage: "Prd", primary: "local:llama3.1:8b", fallback: "claude:claude-opus-5-5", isProjectChoice: false, changedBy: null, changedAt: null },
      { stage: "Code", primary: "claude:claude-opus-5-5", fallback: "none", isProjectChoice: true, changedBy: "taylor@example.com", changedAt: "2026-11-10T09:00:00Z" },
    ],
    ...overrides,
  };
}

function row(stage: string) {
  return screen.getByText(stage, { selector: "p" }).parentElement as HTMLElement;
}

beforeEach(() => {
  mocked.getProjectModels.mockReset();
  mocked.chooseProjectModels.mockReset();
  mocked.resetProjectModels.mockReset();
});

describe("ProjectModels", () => {
  it("shows each stage's models and who chose them", async () => {
    mocked.getProjectModels.mockResolvedValue(models());
    render(<ProjectModels projectId="p1" />);

    expect(await screen.findByText("local:llama3.1:8b → claude:claude-opus-5-5")).toBeInTheDocument();
    expect(within(row("Prd")).getByText("Default")).toBeInTheDocument();
    expect(within(row("Code")).getByText("claude:claude-opus-5-5")).toBeInTheDocument();
    expect(within(row("Code")).getByText(/Chosen by taylor@example.com/)).toBeInTheDocument();
  });

  it("offers no changes to members who are not Tech Leads", async () => {
    mocked.getProjectModels.mockResolvedValue(models({ canChange: false }));
    render(<ProjectModels projectId="p1" />);

    expect(await screen.findByText(/Only a Tech Lead can change them/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Change" })).not.toBeInTheDocument();
  });

  it("saves a Tech Lead's choice, treating an empty fallback as the default", async () => {
    mocked.getProjectModels.mockResolvedValue(models());
    mocked.chooseProjectModels.mockResolvedValue(models());
    render(<ProjectModels projectId="p1" />);

    await userEvent.click(within(await screen.findByText("Prd", { selector: "p" }).then((p) => p.parentElement!)).getByRole("button", { name: "Change" }));
    const primary = screen.getByLabelText("Prd primary model");
    await userEvent.clear(primary);
    await userEvent.type(primary, " local:qwen3:32b ");
    await userEvent.clear(screen.getByLabelText("Prd fallback model"));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(mocked.chooseProjectModels).toHaveBeenCalledWith("p1", "Prd", "local:qwen3:32b", null);
  });

  it("returns a stage to its default", async () => {
    mocked.getProjectModels.mockResolvedValue(models());
    mocked.resetProjectModels.mockResolvedValue(models());
    render(<ProjectModels projectId="p1" />);

    await userEvent.click(await screen.findByRole("button", { name: "Use default" }));

    expect(mocked.resetProjectModels).toHaveBeenCalledWith("p1", "Code");
  });

  it("shows the server's reason when a change is refused", async () => {
    mocked.getProjectModels.mockResolvedValue(models());
    mocked.resetProjectModels.mockRejectedValue(new Error("Only a Tech Lead may change which models a project uses."));
    render(<ProjectModels projectId="p1" />);

    await userEvent.click(await screen.findByRole("button", { name: "Use default" }));

    expect(await screen.findByText("Only a Tech Lead may change which models a project uses.")).toBeInTheDocument();
  });
});
