# Zephyrus on Zephyrus

In March 2026, during development, Zephyrus was pointed at its own repository.
These are the artifacts its agents wrote there, unedited: a real example of
what the PRD, Architect and Task Agents produce.

## Delete projects, features and artifacts

The idea, as entered: *"As a user of Zephyrus UI I need to be able to delete
projects…"*. It went through the pipeline three times in three days, and the
feature shipped on 28 March in
[#48](https://github.com/MichaelGeranis/zephyrus/pull/48): a preview of
everything a deletion removes, then a confirmation.

| Pass | PRD | ADR | Task breakdown |
|------|-----|-----|----------------|
| 1 · 26 March | [`prd/4140d132…`](prd/4140d132-2695-4d8b-a031-027672121549.md) | [`adr/c4f05b03…`](adr/c4f05b03-48b8-4d62-98c0-8052606e2655.md) | [`tasks/dcb62462…`](tasks/dcb62462-73ff-46d6-975b-8a005703d47e.md), 11 tasks |
| 2 · 27 March | [`prd/7424f0da…`](prd/7424f0da-db35-4f9c-84d1-2fed55fa9961.md) | [`adr/6eddd9c9…`](adr/6eddd9c9-0029-43fc-8772-3591e34630cb.md) | [`tasks/b5fffd44…`](tasks/b5fffd44-4106-4dea-81fc-1fdbdbfd9102.md), 14 tasks |
| 3 · 28 March | (pass 2's) | [`adr/8b5cfc12…`](adr/8b5cfc12-1f74-4456-8afb-efca94045808.md) | [`tasks/259adf8d…`](tasks/259adf8d-3a98-4ec7-b01c-4116e64c18ae.md), 3 tasks |

File names are the artifact IDs Zephyrus assigned. The passes show the loop
the approval gates create: a reviewer reads the output, and until it is
right, the step is re-run instead of approved. The third pass cut the work from 14
tasks to 3.

## Health check endpoint

[`prd/health-check-endpoint.md`](prd/health-check-endpoint.md) is the PRD from
the first run against this repository, on 25 March. It was not taken further.

## Note

These files predate later changes to the agent prompts, and the run used the
models configured at the time, so a run today produces different output.
Zephyrus now writes these artifacts under `docs/prd/`, `docs/adr/`, `docs/tasks/`
and `docs/qa/` of the repository it works on, and records which agent run,
model and prompt version produced each one.
