import { expect, test, type Page } from "@playwright/test";
import { demo, signIn, tokens } from "./helpers";

async function openProject(page: Page) {
  await page.goto("/projects");
  await page.getByText(demo.project).click();
  await expect(page.getByRole("heading", { name: "Models" })).toBeVisible();
}

function stageRow(page: Page, stage: string) {
  return page.locator("section[aria-labelledby=models-heading] > div > div").filter({ hasText: new RegExp(`^${stage}`) });
}

test("a PM/EM sees each stage's models but cannot change them", async ({ page }) => {
  await signIn(page, tokens.pm);
  await openProject(page);

  await expect(page.getByText("Only a Tech Lead can change them.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Change" })).toHaveCount(0);
});

test("a Tech Lead chooses a stage's models, the choice is attributed, and can be undone", async ({ page }) => {
  await signIn(page, tokens.techLead);
  await openProject(page);

  await stageRow(page, "Code").getByRole("button", { name: "Change" }).click();
  await page.getByLabel("Code primary model").fill("local:qwen3:32b");
  await page.getByLabel("Code fallback model").fill("none");
  await page.getByRole("button", { name: "Save" }).click();

  await expect(stageRow(page, "Code")).toContainText("local:qwen3:32b");
  await expect(stageRow(page, "Code")).toContainText("Chosen by tech-lead@zephyrus.local");

  await stageRow(page, "Code").getByRole("button", { name: "Use default" }).click();
  await expect(stageRow(page, "Code")).toContainText("Default");
});
