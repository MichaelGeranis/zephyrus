import { expect, test, type Page } from "@playwright/test";
import { demo, openFeature, signIn, tokens } from "./helpers";

function collectErrors(page: Page) {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("console", (m) => {
    if (m.type() === "error") errors.push(m.text());
  });
  return errors;
}

test("every page loads without errors", async ({ page }) => {
  await signIn(page, tokens.all);
  const errors = collectErrors(page);

  await page.getByRole("link", { name: "Dashboard" }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
  await page.waitForLoadState("networkidle");

  await openFeature(page, demo.featureAtIdea);
  await openFeature(page, demo.featureAwaitingPrd);

  // The demo seed runs the PRD Agent, so this feature always has a run to open.
  await page.getByRole("link", { name: "View Prompts" }).first().click();
  await expect(page).toHaveURL(/\/invocations\//);
  await page.waitForLoadState("networkidle");

  await openFeature(page, demo.featureAwaitingPrd);
  await page.getByRole("link", { name: /^(Review & Approve|View)$/ }).first().click();
  await expect(page.getByRole("heading", { name: /^PRD:/ })).toBeVisible();

  expect(errors).toEqual([]);
});

test("the delete dialog explains what would be deleted and can be cancelled", async ({ page }) => {
  await signIn(page, tokens.all);
  await openFeature(page, demo.featureAtIdea);

  await page.getByRole("button", { name: "Delete" }).first().click();
  await expect(page.getByText("This action cannot be undone.")).toBeVisible();
  await page.getByRole("button", { name: "Cancel" }).click();

  await expect(page.getByText("This action cannot be undone.")).toHaveCount(0);
  await openFeature(page, demo.featureAtIdea);
});
