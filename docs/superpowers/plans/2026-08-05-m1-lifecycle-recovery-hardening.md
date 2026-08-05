# M1 Lifecycle Recovery Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the already-failed Task-7 disposable experiment safely recoverable without launching the game or repeating the lifecycle experiment.

**Architecture:** Keep the immutable experiment result separate from the recovery result. Cleanup is driven by recorded side effects: plugin removal is required only after deployment, while loader rollback is required after loader application. Recovery accepts `Failed` ownership only when a valid `RollbackRequired` transaction is bound to the saved disposable baseline and the profile is inactive.

**Tech Stack:** C#/.NET 10, System.Text.Json, existing Task-3 loader transaction services, xUnit repository tests, PowerShell as a thin CLI wrapper only.

---

### Task 1: Establish the failing recovery and cleanup tests

**Files:**
- Modify: `tests/ThroneForge.PluginSmokeTest.Tests/LifecycleOrchestrationTests.cs`
- Modify: `tests/ThroneForge.PluginSmokeTest.Tests/LifecycleProductionStateTests.cs`
- Create or modify: `tests/ThroneForge.PluginSmokeTest.Tests/LifecycleRecoveryTests.cs`

- [ ] **Step 1: Add tests for conditional cleanup evidence**

  Assert that a `LoaderLaunch` failure with `LoaderApplied=true` and `PluginDeployed=false` produces `PluginRemoval = NotRequired`, `LoaderRollback = Required`, and no `plugin-removal-failed` cleanup category.

- [ ] **Step 2: Add tests for failed-ownership recovery eligibility**

  Build a real temporary ownership record with `Status=Failed`, a saved baseline, and a `RollbackRequired` transaction. Assert that recovery accepts it; add negative cases for missing transaction, wrong baseline, active process, and already rolled-back transaction.

- [ ] **Step 3: Run the focused tests and verify the new tests fail for the current implementation**

  Run:

  ```powershell
  dotnet test tests/ThroneForge.PluginSmokeTest.Tests -c Release --filter "FullyQualifiedName~LifecycleOrchestrationTests|FullyQualifiedName~LifecycleRecoveryTests|FullyQualifiedName~LifecycleProductionStateTests"
  ```

  Expected: the new assertions fail because cleanup is unconditional and `Failed` ownership is rejected.

### Task 2: Add explicit cleanup applicability and preserve failed experiment state

**Files:**
- Modify: `src/ThroneForge.PluginSmokeTest/LifecycleExperimentOrchestrator.cs`
- Modify: `src/ThroneForge.PluginSmokeTest/LifecycleExperimentProductionOperations.cs`
- Modify: `src/ThroneForge.PluginSmokeTest/LifecycleExperimentReportWriter.cs`

- [ ] **Step 1: Add a typed cleanup status**

  Represent `NotRequired`, `Passed`, and `Failed` separately for plugin removal and loader rollback. Apply evidence before validity classification so `LoaderApplied` and `PluginDeployed` survive failed evidence.

- [ ] **Step 2: Make cleanup side-effect driven**

  Run plugin removal only when the accumulator contains `PluginDeployed=true` and a plugin root. Run loader rollback only when `LoaderApplied=true`. Record `NotRequired` otherwise and never classify it as a failure.

- [ ] **Step 3: Preserve immutable primary failure data**

  Keep the original `LoaderLaunch/loader-launch-failed` pair unchanged while cleanup stages add independent cleanup results. Generate the report from the separate experiment and recovery/cleanup sections.

- [ ] **Step 4: Run the focused orchestration tests**

  Run the Task-7 focused test filter and require the new cleanup cases plus existing lifecycle tests to pass.

### Task 3: Harden rollback eligibility and generated-evidence drift checks

**Files:**
- Modify: `src/ThroneForge.PluginSmokeTest/LifecycleExperimentRecoveryService.cs`
- Modify: `src/ThroneForge.LoaderSmokeTest/LoaderTransactionStateService.cs`
- Create or modify: `src/ThroneForge.LoaderSmokeTest/LoaderRollbackProfileVerificationService.cs`
- Modify: `tests/ThroneForge.LoaderSmokeTest.Tests/LoaderOnlyProfileVerificationTests.cs`
- Modify: `tests/ThroneForge.PluginSmokeTest.Tests/LifecycleRecoveryTests.cs`

- [ ] **Step 1: Add rollback-specific comparison tests**

  Cover changed approved logs/config/cache as allowed, changed loader core/game files as rejected, arbitrary added files as rejected, and exact baseline restoration after rollback.

- [ ] **Step 2: Implement the bounded runtime-difference comparator**

  Compare the current disposable manifest to `ExpectedAppliedManifest`, allowing only approved generated-evidence files/directories and rejecting transaction, loader core, Doorstop, game, reparse-point, and arbitrary-file changes.

- [ ] **Step 3: Permit only safe `Failed` recovery**

  Require ownership validity, expected fingerprint, canonical baseline, valid transaction binding, status `Applied`, `LaunchObserved`, or `RollbackRequired`, and no active process. Keep `RolledBack` and `FailedAndRolledBack` rejected.

- [ ] **Step 4: Return stable recovery phases**

  Distinguish ownership, transaction, process, runtime drift, plugin removal, loader rollback, baseline restoration, disposable readiness, and original postcheck failures without replacing the immutable experiment category.

### Task 4: Update report, CLI recovery output, tests, and documentation

**Files:**
- Modify: `src/ThroneForge.PluginSmokeTest/Program.cs`
- Modify: `src/ThroneForge.PluginSmokeTest/LifecycleExperimentReportWriter.cs`
- Modify: `docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-lifecycle-binding.md`
- Modify: `PLAN.md`
- Modify: `STATUS.md`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/discovery/README.md`

- [ ] **Step 1: Add recovery-only CLI evidence**

  Emit `rollback-result`, `loader-rollback-verified`, `disposable-restored`, `original-verified`, and stable recovery category; never emit a lifecycle success claim.

- [ ] **Step 2: Extend the sanitized report**

  Record the immutable final experiment failure separately from recovery status, including `PluginRemoval=NotRequired` for the pre-deployment failure.

- [ ] **Step 3: Update documentation without reclassifying Task 7**

  State that the lifecycle experiment remains `Failed`, recovery is a separate cleanup action, and Task 8 remains blocked.

### Task 5: Validate and perform the single authorized recovery-only action

- [ ] **Step 1: Run local restore, format, Release build, tests, diff check, and hygiene checks.**
- [ ] **Step 2: Push the repository-only correction and run exact-SDK hosted CI.**
- [ ] **Step 3: Download and parse all hosted TRX artifacts; require zero failures, skips, aborts, inconclusive and not-executed results.**
- [ ] **Step 4: Execute exactly one `-Mode Rollback` against the existing failed experiment root. Do not launch the game and do not create a new experiment.**
- [ ] **Step 5: Record the recovery result, commit only sanitized documentation, run final hosted CI, and stop.**

## Self-review

- The plan never repeats the lifecycle experiment or begins Task 8.
- The original experiment result remains immutable even if recovery succeeds.
- Cleanup is conditional on measured side effects.
- Rollback uses the existing canonical Task-3/Task-6 state services and exact baseline restoration.
- Private state, paths, logs, binaries, and manifests remain external.
