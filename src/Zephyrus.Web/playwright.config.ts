import { defineConfig, devices } from "@playwright/test";

/**
 * End-to-end tests against a running demo stack (`docker compose up`). They
 * share its seeded data and some tests change it (approving the demo PRD), so
 * they run one at a time, in order, against a fresh stack.
 */
export default defineConfig({
  testDir: "e2e",
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: "list",
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:3000",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: {
        ...devices["Desktop Chrome"],
        // Set when a preinstalled Chromium should be used instead of the one
        // `npx playwright install` downloads.
        launchOptions: process.env.E2E_CHROMIUM_PATH
          ? { executablePath: process.env.E2E_CHROMIUM_PATH }
          : {},
      },
    },
  ],
});
