import { expect, type Page } from "@playwright/test";

/** Demo team tokens from docker-compose.yml. */
export const tokens = {
  pm: "demo-pm",
  techLead: "demo-tech-lead",
  qa: "demo-qa",
  all: "demo-all",
} as const;

export const demo = {
  project: "Acme Billing (demo)",
  featureAwaitingPrd: "Let customers download their invoices as PDF",
  featureAtIdea: "Send a weekly usage summary",
};

/** Signs in from the header; the page reloads with the new credentials. */
export async function signIn(page: Page, token: string) {
  await page.goto("/projects");
  await page.getByPlaceholder("Team token").fill(token);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByText("Signed in as")).toBeVisible();
}

export async function openFeature(page: Page, title: string) {
  await page.goto("/projects");
  await page.getByText(demo.project).click();
  await page.getByText(title).click();
  await expect(page.getByRole("heading", { level: 1, name: title })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Artifacts" })).toBeVisible();
}
