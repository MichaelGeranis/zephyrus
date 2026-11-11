import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApprovalGate } from "@/components/ApprovalGate";
import { api } from "@/lib/api";
import { getTeamToken, setTeamToken } from "@/lib/auth";
import { artifact, pm } from "../fixtures";

vi.mock("@/lib/api", async (importOriginal) => ({
  ApiError: (await importOriginal<typeof import("@/lib/api")>()).ApiError,
  api: { getCurrentUser: vi.fn(), approveArtifact: vi.fn(), updateArtifactContent: vi.fn() },
}));

const mocked = vi.mocked(api);
const content = "# PRD: Invoice PDFs\n\nCustomers can **download** invoices.";

beforeEach(() => {
  mocked.getCurrentUser.mockReset();
  mocked.approveArtifact.mockReset();
  mocked.updateArtifactContent.mockReset();
});

function renderGate(onApproved = vi.fn(), overrides = {}) {
  render(
    <ApprovalGate featureId="feature-1" artifact={artifact(overrides)} content={content} onApproved={onApproved} />,
  );
  return onApproved;
}

async function signedIn() {
  setTeamToken("demo-pm");
  mocked.getCurrentUser.mockResolvedValue(pm);
}

describe("ApprovalGate", () => {
  it("renders the artifact as markdown", () => {
    renderGate();

    expect(screen.getByRole("heading", { name: "PRD: Invoice PDFs" })).toBeInTheDocument();
    expect(screen.getByText("download").tagName).toBe("STRONG");
  });

  it("renders the tables and task lists agents write", () => {
    render(
      <ApprovalGate
        featureId="feature-1"
        artifact={artifact()}
        content={"| Area | Estimate |\n|------|----------|\n| Backend | M |\n\n- [ ] Migration is reversible"}
        onApproved={vi.fn()}
      />,
    );

    expect(screen.getByRole("table")).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Backend" })).toBeInTheDocument();
    expect(screen.getByRole("checkbox")).not.toBeChecked();
  });

  it("approves as the signed-in member and reports the approved artifact", async () => {
    await signedIn();
    const approved = artifact({ approvedBy: "pat@example.com", approvedAt: "2026-11-09T10:00:00Z" });
    mocked.approveArtifact.mockResolvedValue(approved);

    const onApproved = renderGate();
    expect(await screen.findByText("Pat")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Approve" }));

    expect(mocked.approveArtifact).toHaveBeenCalledWith("feature-1", "artifact-1");
    expect(onApproved).toHaveBeenCalledWith(approved);
  });

  it("shows the server's reason when approval is refused", async () => {
    await signedIn();
    mocked.approveArtifact.mockRejectedValue(new Error("Only a PM/EM may approve a PRD."));

    const onApproved = renderGate();
    await userEvent.click(await screen.findByRole("button", { name: "Approve" }));

    expect(await screen.findByText("Only a PM/EM may approve a PRD.")).toBeInTheDocument();
    expect(onApproved).not.toHaveBeenCalled();
  });

  it("asks a signed-out member to sign in before approving", async () => {
    renderGate();

    expect(screen.queryByRole("button", { name: "Approve" })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(screen.getByText("A team token is required.")).toBeInTheDocument();
  });

  it("signs in from the approval panel", async () => {
    mocked.getCurrentUser.mockResolvedValue(pm);
    renderGate();

    await userEvent.type(screen.getByLabelText("Team token"), "demo-pm");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("button", { name: "Approve" })).toBeInTheDocument();
    expect(getTeamToken()).toBe("demo-pm");
  });

  it("does not keep a token the API rejects", async () => {
    mocked.getCurrentUser.mockRejectedValue(new Error("401"));
    renderGate();

    await userEvent.type(screen.getByLabelText("Team token"), "wrong");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByText("That token was not recognised.")).toBeInTheDocument();
    expect(getTeamToken()).toBeNull();
  });

  it("shows who approved an already approved artifact and offers no approval", () => {
    renderGate(vi.fn(), { approvedBy: "pat@example.com", approvedAt: "2026-11-09T10:00:00Z" });

    expect(screen.getByText(/Approved by pat@example.com/)).toBeInTheDocument();
    expect(screen.queryByText("Approve this artifact")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
  });

  it("saves edited content", async () => {
    mocked.updateArtifactContent.mockResolvedValue(artifact());
    renderGate();

    await userEvent.click(screen.getByRole("button", { name: "Edit" }));
    expect(screen.queryByRole("button", { name: "Save" })).not.toBeInTheDocument();
    await userEvent.type(screen.getByRole("textbox"), " Edited.");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(mocked.updateArtifactContent).toHaveBeenCalledWith("feature-1", "artifact-1", `${content} Edited.`);
    expect(await screen.findByText("Saved")).toBeInTheDocument();
  });

  it("previews edits before they are saved", async () => {
    renderGate();

    await userEvent.click(screen.getByRole("button", { name: "Edit" }));
    await userEvent.type(screen.getByRole("textbox"), "\n\n## New section");
    await userEvent.click(screen.getByRole("button", { name: "Preview" }));

    expect(screen.getByRole("heading", { name: "New section" })).toBeInTheDocument();
  });
});
