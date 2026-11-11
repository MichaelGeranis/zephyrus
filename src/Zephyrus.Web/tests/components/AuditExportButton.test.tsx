import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AuditExportButton } from "@/components/AuditExportButton";

describe("AuditExportButton", () => {
  it("downloads the audit trail when clicked", async () => {
    const download = vi.fn().mockResolvedValue(undefined);
    render(<AuditExportButton download={download} />);

    await userEvent.click(screen.getByRole("button", { name: "Export audit trail" }));

    expect(download).toHaveBeenCalledOnce();
    expect(screen.getByRole("button", { name: "Export audit trail" })).toBeEnabled();
  });

  it("shows why the export failed", async () => {
    const download = vi.fn().mockRejectedValue(new Error("Feature not found."));
    render(<AuditExportButton download={download} />);

    await userEvent.click(screen.getByRole("button", { name: "Export audit trail" }));

    expect(await screen.findByText("Feature not found.")).toBeInTheDocument();
  });
});
