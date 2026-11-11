import { expect, test } from "@playwright/test";
import { demo, openFeature, signIn, tokens } from "./helpers";

/**
 * The PRD approval gate, end to end. These run in order: the QA refusal must
 * happen while the demo PRD is still awaiting approval.
 */
test.describe.serial("PRD approval gate", () => {
  test("only a PM/EM may approve a PRD: QA is refused and nothing moves", async ({ page }) => {
    await signIn(page, tokens.qa);
    await openFeature(page, demo.featureAwaitingPrd);
    await page.getByRole("link", { name: "Review & Approve" }).click();

    await expect(page.getByRole("heading", { name: /^PRD:/ })).toBeVisible();
    await expect(page.getByText("Quinn (QA)").last()).toBeVisible();
    await page.getByRole("button", { name: "Approve" }).click();

    await expect(page.getByText(/requires the PmEm role\. Caller holds: Qa\./)).toBeVisible();

    await openFeature(page, demo.featureAwaitingPrd);
    await expect(page.getByText("PrdPending").first()).toBeVisible();
    await expect(page.getByRole("heading", { name: "Agent Jobs" })).toHaveCount(0);
  });

  test("a PM/EM approves the PRD: the feature advances, the Architect Agent is queued and the approver is recorded", async ({
    page,
  }) => {
    await signIn(page, tokens.pm);
    await openFeature(page, demo.featureAwaitingPrd);
    await page.getByRole("link", { name: "Review & Approve" }).click();

    await expect(page.getByText("Approving as")).toContainText("Pat (PM/EM)");
    await page.getByRole("button", { name: "Approve" }).click();
    await expect(page.getByText(/Approved by pm@zephyrus\.local/)).toBeVisible();

    await openFeature(page, demo.featureAwaitingPrd);
    await expect(page.getByRole("heading", { name: "Agent Jobs" })).toBeVisible();
    await expect(page.getByText("Architect Agent").first()).toBeVisible();
    // The demo Architect Agent finishes quickly; the feature then waits for the Tech Lead.
    await expect(page.getByText("ArchPending").first()).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText("pm@zephyrus.local").first()).toBeVisible();
  });
});
