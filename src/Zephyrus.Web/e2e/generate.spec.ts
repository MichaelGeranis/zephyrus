import { expect, test } from "@playwright/test";
import { demo, openFeature, signIn, tokens } from "./helpers";

// Runs after browse.spec.ts, which only reads the idea-stage feature.
test("Generate PRD queues the PRD Agent, which writes the PRD in the background", async ({ page }) => {
  await signIn(page, tokens.pm);
  await openFeature(page, demo.featureAtIdea);

  await page.getByRole("button", { name: "Generate PRD" }).click();

  // The request returns once the job is queued; the page follows the job.
  await expect(page.getByRole("heading", { name: "Agent Jobs" })).toBeVisible();
  await expect(page.getByText("Prd Agent")).toBeVisible();
  await expect(page.getByRole("link", { name: "Review & Approve" })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText("PrdPending").first()).toBeVisible();
});
