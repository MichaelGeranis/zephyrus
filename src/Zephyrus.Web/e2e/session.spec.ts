import { expect, test } from "@playwright/test";
import { demo, signIn, tokens } from "./helpers";

test("a signed-out visitor is asked to sign in and sees no data", async ({ page }) => {
  await page.goto("/projects");

  await expect(page.getByRole("heading", { name: "Sign in to Zephyrus" })).toBeVisible();
  await expect(page.getByText(demo.project)).toHaveCount(0);
});

test("an unknown token is rejected", async ({ page }) => {
  await page.goto("/projects");
  await page.getByPlaceholder("Team token").fill("not-a-real-token");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page.getByText("Token not recognised.")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Sign in to Zephyrus" })).toBeVisible();
});

test("a team member signs in, sees the projects and signs out", async ({ page }) => {
  await signIn(page, tokens.pm);

  await expect(page.getByText("Pat (PM/EM)")).toBeVisible();
  await expect(page.getByText(demo.project)).toBeVisible();

  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page.getByRole("heading", { name: "Sign in to Zephyrus" })).toBeVisible();
});
