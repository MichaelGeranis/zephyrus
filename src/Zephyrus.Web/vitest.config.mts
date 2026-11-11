import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

/**
 * Component and unit tests. They run in jsdom with the API module mocked,
 * so no backend is needed; end-to-end tests live in e2e/ and run with
 * Playwright against the Docker demo stack.
 */
export default defineConfig({
  resolve: {
    alias: { "@": fileURLToPath(new URL(".", import.meta.url)) },
  },
  test: {
    environment: "jsdom",
    include: ["tests/**/*.test.{ts,tsx}"],
    setupFiles: ["tests/setup.ts"],
    restoreMocks: true,
    // The gate covers the code these tests own: components, lib and the
    // proxy. Pages are exercised end to end by Playwright (e2e/).
    coverage: {
      provider: "v8",
      include: ["components/**/*.{ts,tsx}", "lib/**/*.ts", "proxy.ts"],
      reporter: ["text-summary", "text"],
      thresholds: { lines: 80, statements: 80, branches: 80, functions: 80 },
    },
  },
});
