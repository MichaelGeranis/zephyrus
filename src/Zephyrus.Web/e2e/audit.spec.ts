import { readFile } from "node:fs/promises";
import { expect, test } from "@playwright/test";
import { demo, openFeature, signIn, tokens } from "./helpers";

// Runs after approval-gate.spec.ts, so the feature has an approved PRD and an ADR.
test("the audit trail downloads with each artifact traced to the run that produced it", async ({ page }) => {
  await signIn(page, tokens.qa);
  await openFeature(page, demo.featureAwaitingPrd);

  const [download] = await Promise.all([
    page.waitForEvent("download"),
    page.getByRole("button", { name: "Export audit trail" }).click(),
  ]);
  expect(download.suggestedFilename()).toMatch(/^zephyrus-audit-feature-[0-9a-f-]+\.json$/);

  const audit = JSON.parse(await readFile((await download.path())!, "utf8"));
  expect(audit.format).toBe("zephyrus.audit/v1");
  expect(audit.exportedBy).toBe("qa@zephyrus.local");

  const [feature] = audit.features;
  const prd = feature.artifacts.find((a: { type: string }) => a.type === "Prd");
  expect(prd.approvedBy).toBe("pm@zephyrus.local");
  const run = feature.agentRuns.find((r: { id: string }) => r.id === prd.producedBy[0]);
  expect(run.agent).toBe("prd");
  expect(run.promptVersion).toMatch(/^[0-9a-f]{12}$/);
  expect(
    feature.pipelineEvents.some(
      (e: { to: string; triggeredBy: string }) => e.to === "PrdApproved" && e.triggeredBy === "pm@zephyrus.local",
    ),
  ).toBe(true);
});
