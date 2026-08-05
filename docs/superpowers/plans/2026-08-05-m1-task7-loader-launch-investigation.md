# M1 Task 7 Loader Launch Investigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add repository-only diagnostics that preserve bounded BepInEx LoaderLaunch evidence and identify sanitized rollback-manifest drift without rerunning the private experiment.

**Architecture:** Extend the existing Task-3 loader result with a structured launch diagnostic derived from the observed process and sanitized log summary. Replace the recovery drift exception-only path with a bounded relative-path difference result that distinguishes added, removed, changed, and directory differences. Propagate both records through Task 7's typed production evidence and report writer; do not read or reconstruct the untrusted historical experiment directory.

**Tech Stack:** .NET 10, C#, xUnit, existing `PEReader`/manifest and Task-3/Task-7 services, sanitized Markdown reports.

---

### Task 1: Record the investigation boundary and inspect the existing data flow

**Files:**
- Modify: `PLAN.md`
- Read: `docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-lifecycle-binding.md`
- Read: `src/ThroneForge.LoaderSmokeTest/SmokeTestOrchestrator.cs`
- Read: `src/ThroneForge.LoaderSmokeTest/LoaderTransactionStateService.cs`
- Read: `src/ThroneForge.PluginSmokeTest/LifecycleExperimentProductionOperations.cs`
- Read: `src/ThroneForge.PluginSmokeTest/LifecycleExperimentRecoveryService.cs`

- [ ] Add a current branch entry to `PLAN.md` stating that this slice is repository-only, investigates LoaderLaunch/preloader evidence and recovery drift, does not rerun the private profile, does not reuse the old experiment root, and keeps Task 8 blocked.
- [ ] Record the confirmed historical limitation: the committed report has no raw loader log or persisted file-level drift list, so the exact historical file cannot be named from repository evidence.
- [ ] Confirm with `rg` that no implementation or test reads the old external experiment path.

### Task 2: Add failing tests for LoaderLaunch diagnostics

**Files:**
- Modify: `tests/ThroneForge.LoaderSmokeTest.Tests/LoaderLogParserTests.cs`
- Modify: `tests/ThroneForge.PluginSmokeTest.Tests/LifecycleProductionStateTests.cs`
- Modify: `tests/ThroneForge.PluginSmokeTest.Tests/LifecycleOrchestrationTests.cs`

- [ ] Test that a failed launch result preserves sanitized launch facts: process-started, process-exited, executable-contained, exit-code presence, launch category, log readability, BepInEx version, preloader/chainloader flags, plugin count, warning/error/fatal counts, and no raw log text or path.
- [ ] Test that an incomplete bootstrap distinguishes `process-exited-during-observation`, `log-missing`, and `bootstrap-evidence-invalid` without claiming the exact private root cause.
- [ ] Test that production `LoaderLaunch` transfers the structured diagnostic from `SmokeTestExecutionResult` into `LoaderModeExecutionEvidence` and then into `LifecycleExperimentResult`.
- [ ] Run the focused tests and verify they fail because the diagnostic fields and propagation do not yet exist.

### Task 3: Add failing tests for bounded rollback drift evidence

**Files:**
- Modify: `tests/ThroneForge.LoaderSmokeTest.Tests/LoaderTransactionServiceTests.cs`
- Modify: `tests/ThroneForge.PluginSmokeTest.Tests/LifecycleRecoveryTests.cs`

- [ ] Test a changed loader/core file, removed loader file, added arbitrary file, missing directory, and unexpected directory; assert a structured relative-path difference category rather than only an exception message.
- [ ] Test that approved generated differences under `BepInEx/LogOutput.*`, `BepInEx/config/**`, and `BepInEx/cache/**` are classified as allowed and do not produce `recovery-runtime-drift`.
- [ ] Test that drift evidence is bounded, uses relative forward-slash paths only, contains no absolute path, username, machine name, raw manifest, or raw hash dump, and remains available in the recovery result.
- [ ] Run the focused tests and verify they fail because recovery currently discards the comparison details.

### Task 4: Implement LoaderLaunch diagnostic evidence

**Files:**
- Modify: `src/ThroneForge.LoaderSmokeTest/SmokeTestModels.cs`
- Modify: `src/ThroneForge.LoaderSmokeTest/SmokeTestOrchestrator.cs`
- Modify: `src/ThroneForge.PluginSmokeTest/LifecycleExperimentOrchestrator.cs`
- Modify: `src/ThroneForge.PluginSmokeTest/LifecycleExperimentProductionOperations.cs`

- [ ] Add an immutable `LoaderLaunchDiagnosticEvidence` containing only bounded booleans, a nullable numeric exit code, stable categories, sanitized BepInEx version/counters, and no raw log or filesystem path.
- [ ] Build it from the actual `LaunchObservationResult`, `LoaderLogSummary`, and bootstrap result in `LaunchInstalled`; preserve it for missing-log and failed-preloader paths.
- [ ] Carry it through `LoaderModeExecutionEvidence`, the accumulator, and `LifecycleExperimentResult`; require no diagnostic field for historical states where it was never retained.
- [ ] Keep the classification fail-closed: diagnostic evidence explains the observed boundary but cannot turn incomplete preloader evidence into success.

### Task 5: Implement structured rollback drift evidence

**Files:**
- Modify: `src/ThroneForge.LoaderSmokeTest/SmokeTestModels.cs`
- Modify: `src/ThroneForge.LoaderSmokeTest/LoaderTransactionStateService.cs`
- Modify: `src/ThroneForge.PluginSmokeTest/LifecycleExperimentRecoveryService.cs`
- Modify: `src/ThroneForge.PluginSmokeTest/LifecycleExperimentReportWriter.cs`

- [ ] Add a bounded `RollbackDriftEvidence` with status, stable difference categories, and sanitized relative paths only.
- [ ] Add a non-throwing comparison/classification method over the existing `ManifestVerificationResult`; retain the existing fail-closed mutation gate and approved generated-evidence policy.
- [ ] Attach the evidence to `LifecycleExperimentRollbackResult` when `recovery-runtime-drift` occurs, including the concrete safe relative path and difference kind when retained by the current run.
- [ ] Keep historical report wording honest: because the old private run did not retain this record, it must say the exact historical drift file was not retained rather than inventing one.

### Task 6: Documentation and repository-only validation

**Files:**
- Modify: `docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-lifecycle-binding.md`
- Modify: `docs/discovery/README.md`
- Modify: `STATUS.md`
- Modify: `README.md`
- Modify: `CHANGELOG.md`

- [ ] Document that this branch adds diagnostics only; the final authorized experiment remains `Failed` at `LoaderLaunch`, lifecycle binding remains unverified, and the old experiment root is untrusted and unused.
- [ ] Document that future runs can report bounded launch facts and relative drift categories, while the historical file-level drift remains unavailable.
- [ ] Run `dotnet --version`, locked restore, format verification where the pinned SDK is available, Release build, complete tests, `git diff --check`, and hygiene scans.
- [ ] Do not launch Thronefall/BepInEx, do not invoke rollback, and do not create or commit private state, logs, binaries, manifests, or paths.
