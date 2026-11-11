import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { DeleteButton } from "@/components/ui/DeleteButton";
import { DeleteConfirmationModal } from "@/components/ui/DeleteConfirmationModal";

const preview = { entityTitle: "Acme Billing", childrenCount: 3, warnings: ["Artifacts stay in GitHub."] };

function renderModal(overrides: Partial<Parameters<typeof DeleteConfirmationModal>[0]> = {}) {
  const props = {
    entityType: "project",
    fetchPreview: vi.fn().mockResolvedValue(preview),
    onConfirm: vi.fn().mockResolvedValue(undefined),
    onCancel: vi.fn(),
    ...overrides,
  };
  render(<DeleteConfirmationModal {...props} />);
  return props;
}

describe("DeleteConfirmationModal", () => {
  it("shows what will be deleted", async () => {
    renderModal();

    expect(screen.getByText("Delete project?")).toBeInTheDocument();
    expect(await screen.findByText(/Acme Billing/)).toBeInTheDocument();
    expect(screen.getByText(/associated items/)).toHaveTextContent("This will also delete 3 associated items.");
    expect(screen.getByText("Artifacts stay in GitHub.")).toBeInTheDocument();
  });

  it("does not allow deleting until the preview has loaded", () => {
    renderModal({ fetchPreview: vi.fn(() => new Promise<typeof preview>(() => {})) });

    expect(screen.getByText("Loading details...")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Delete" })).toBeDisabled();
  });

  it("deletes on confirm", async () => {
    const { onConfirm } = renderModal();

    await screen.findByText(/Acme Billing/);
    await userEvent.click(screen.getByRole("button", { name: "Delete" }));

    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it("cancels without deleting", async () => {
    const { onCancel, onConfirm } = renderModal();

    await userEvent.click(screen.getByRole("button", { name: "Cancel" }));

    expect(onCancel).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("reports a failed deletion and allows another try", async () => {
    renderModal({ onConfirm: vi.fn().mockRejectedValue(new Error("500")) });

    await screen.findByText(/Acme Billing/);
    await userEvent.click(screen.getByRole("button", { name: "Delete" }));

    expect(await screen.findByText("Deletion failed. Please try again.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Delete" })).toBeEnabled();
  });

  it("reports a preview that fails to load", async () => {
    renderModal({ fetchPreview: vi.fn().mockRejectedValue(new Error("404")) });

    expect(await screen.findByText("Failed to load deletion details.")).toBeInTheDocument();
  });
});

describe("DeleteButton", () => {
  it("is disabled while deleting", () => {
    render(<DeleteButton onClick={vi.fn()} loading />);

    expect(screen.getByRole("button")).toBeDisabled();
    expect(screen.getByRole("button")).toHaveTextContent("Deleting...");
  });

  it("calls back when clicked", async () => {
    const onClick = vi.fn();
    render(<DeleteButton onClick={onClick} label="Delete feature" />);

    await userEvent.click(screen.getByRole("button", { name: "Delete feature" }));

    expect(onClick).toHaveBeenCalledOnce();
  });
});
