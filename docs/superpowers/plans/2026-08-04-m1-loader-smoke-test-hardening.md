# M1 Task 3 Loader Smoke-Test Hardening

> **Execution note:** Implement this plan in the current repository session, with test-first checkpoints after each safety boundary.

## Goal

Make the reusable Task 3 harness fail closed around post-experiment verification, disposable-copy lifecycle, rollback, and committed report output. Preserve the historical private result honestly and do not rerun the private loader experiment unless a regression requires it.

## Constraints

- Do not inspect or modify the local Thronefall installation during implementation.
- Do not install or execute BepInEx, add a plugin, add Harmony, or begin M1 Task 4.
- Do not commit game files, loader binaries, archives, raw logs, recovery markers, or private paths.
- Reuse the existing Discovery runtime/fingerprint services and path protections.

## Design

1. Extend the manifest model with deterministic complete-manifest comparisons and a schema-versioned disposable baseline envelope.
2. Add structured original pre/post verification snapshots that compare fingerprint v1, complete manifests, runtime readiness, loader indicators, backend, architecture, Unity evidence, and TFM assessment.
3. Refactor `Full` into fresh-copy and explicit manifest-backed resume paths. Existing copies are never silently adopted as clean baselines.
4. Add a post-apply guard with injectable launch, log, parse, and report operations. Every non-running failure path rolls back; an active process produces an explicit recovery marker and `Inconclusive` result.
5. Derive the committed report path from the validated repository root and expected fingerprint; reject arbitrary destinations and reparse-point parents.
6. Update the report model, documentation, and changelog to distinguish historical evidence limitations from new harness guarantees.

## Review follow-up corrections

The next correction pass is limited to three fail-closed gaps found after the initial hardening:

1. Derive the original-installation verification sentence from structured post-check state, including failed categories and deferred manual-closure state.
2. Require a saved, schema-valid baseline for every staged mode; only `Prepare` and a fresh `Full` run may create one, while post-install modes must use an explicit transaction state.
3. Preserve recovery-marker persistence failures in structured results and expose a sanitized manual rollback instruction when the active process prevents rollback.

The private loader experiment remains historical evidence only and is not rerun for this correction pass.

## Checkpoints

- [ ] Add failing tests for manifest comparison and schema-backed resume validation.
- [ ] Add failing tests for original post-check/readiness and structured report sections.
- [ ] Add failing tests for post-apply rollback guard and recovery state.
- [ ] Add failing tests for committed report-path containment and CLI rejection of arbitrary paths.
- [ ] Implement the smallest production changes needed to make each checkpoint pass.
- [ ] Run the canonical local validation commands and tracked-file hygiene checks.
- [ ] Push the branch and inspect the hosted Windows/Linux run and all TRX artifacts.
- [ ] Update `STATUS.md` with the actual current-head hosted run; leave M1 Task 4 unstarted.
- [ ] Add regression tests for evidence-derived report claims, staged-mode baseline requirements, and recovery-marker persistence outcomes.
- [ ] Implement the three review follow-up corrections without changing loader behavior or rerunning the private experiment.
