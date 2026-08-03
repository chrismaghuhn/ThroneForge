# M1 Loader Bootstrap Smoke-Test Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Evaluate BepInEx 5.4.23.5 bootstrap initialization for the documented game fingerprint in an external, disposable copy without modifying the original installation or adding a plugin.

**Architecture:** Extend the external-tool boundary with a small `ThroneForge.LoaderSmokeTest` project and synthetic test project. The tool consumes the existing non-writing fingerprint snapshot and runtime-readiness APIs, while isolated pure services validate roots, copy manifests, ZIP entries, transactions, launch evidence, log summaries, and sanitized report data. The private harness is a PowerShell wrapper around these services and never scans for a game path.

**Tech Stack:** C# `net10.0`, framework `System.IO.Compression`, `System.Diagnostics`, SHA-256, xUnit synthetic fixtures, PowerShell, GitHub CLI for official release metadata.

---

### Task 1: Record the merged baseline and scope

**Files:**
- Modify: `PLAN.md`
- Modify: `STATUS.md`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Create: `docs/superpowers/plans/2026-08-03-m1-loader-smoke-test.md`

- [x] **Step 1: Verify the starting branch and merge commit**

Run:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
```

Expected: clean `agent/m1-loader-smoke-test` at `d3f1bb4fde9f77efbb84349f440385cc89002c86` before documentation edits.

- [x] **Step 2: Replace stale Task-2 branch wording**

Record PR #2, merge commit `d3f1bb4fde9f77efbb84349f440385cc89002c86`, main run `30853440786`, Windows/Ubuntu `10 TRX` and `99 tests` per runner, and state that Task 3 has started separately while no loader result exists.

- [x] **Step 3: Commit the scope-only documentation checkpoint**

Run:

```powershell
git add PLAN.md STATUS.md README.md CHANGELOG.md docs/superpowers/plans/2026-08-03-m1-loader-smoke-test.md
git commit -m "docs: start M1 loader smoke test"
```

### Task 2: Define the synthetic-test API

**Files:**
- Create: `src/ThroneForge.LoaderSmokeTest/ThroneForge.LoaderSmokeTest.csproj`
- Create: `tests/ThroneForge.LoaderSmokeTest.Tests/ThroneForge.LoaderSmokeTest.Tests.csproj`
- Modify: `ThroneForge.slnx`
- Modify: `tests/ThroneForge.ArchitectureTests/ArchitectureBoundaryTests.cs`
- Test: `tests/ThroneForge.LoaderSmokeTest.Tests/*.cs`

- [x] **Step 1: Add failing tests for root validation, archive validation, transaction state, and log parsing**

Use synthetic temporary directories and ZIP archives. Assert the public pure APIs reject repository/game/reparse roots, traversal/absolute/duplicate/symlink archive entries, and failed transactions; assert synthetic log summaries expose version, preloader, chainloader, zero plugins, and fatal errors without raw paths.

- [x] **Step 2: Run the new focused tests and observe the expected missing-type failures**

Run:

```powershell
dotnet test tests/ThroneForge.LoaderSmokeTest.Tests -c Release
```

Expected: compile failures because the loader-smoke production types do not exist yet. Do not write production code before this red checkpoint.

- [x] **Step 3: Add the two projects with no forbidden dependency**

Target both projects at `net10.0`. The production tool may reference only `ThroneForge.Discovery`; the test project references only the production tool and existing test packages. Add the projects to `ThroneForge.slnx`, the explicit architecture allowlist, and the expected project-reference map.

### Task 3: Implement safe roots and complete copy manifests

**Files:**
- Create: `src/ThroneForge.LoaderSmokeTest/SmokeTestModels.cs`
- Create: `src/ThroneForge.LoaderSmokeTest/SmokeTestPathValidator.cs`
- Create: `src/ThroneForge.LoaderSmokeTest/InstallationCopyService.cs`
- Test: `tests/ThroneForge.LoaderSmokeTest.Tests/SmokeTestPathValidatorTests.cs`
- Test: `tests/ThroneForge.LoaderSmokeTest.Tests/InstallationCopyServiceTests.cs`

- [x] **Step 1: Make root-boundary tests pass**

Canonicalize explicit absolute roots and reject an experiment root inside the repository, inside the original game root, equal to either root, or reached through existing reparse points. Use separator-aware OS-specific comparisons. Do not create directories until all checks pass. Expose a cleanup validator that accepts only paths below the validated experiment root.

- [x] **Step 2: Make copy-manifest tests pass**

Copy every regular file below the source root without following reparse points. Open each source once for bounded copy/hash, fail closed on any inaccessible or reparse entry, write only to a temporary destination before committing the copy, and record sorted relative paths, sizes, and SHA-256 in an external-only manifest. Verify the copied fingerprint using `InstallationFingerprintService.Capture` before any loader step.

- [x] **Step 3: Verify the focused copy tests**

Run the focused test project. Expected: all root/copy tests pass, including deterministic manifests, partial-copy failure, reparse rejection, and copied-fingerprint mismatch gating.

### Task 4: Implement secure archive inspection and extraction

**Files:**
- Create: `src/ThroneForge.LoaderSmokeTest/ArchiveSafetyService.cs`
- Test: `tests/ThroneForge.LoaderSmokeTest.Tests/ArchiveSafetyServiceTests.cs`

- [x] **Step 1: Add failing synthetic ZIP cases**

Cover absolute names, `..` traversal, device/alternate-data-stream names, backslash/normalization escapes, duplicate normalized destinations, symlink attributes, expanded-size and entry-count limits, and extraction containment.

- [x] **Step 2: Implement deterministic validation and extraction**

Normalize archive names to forward-slash relative paths, reject unsafe names and symlink entries, enforce bounded counts/sizes, compare paths with the target-root boundary, and write an external extraction manifest containing only relative path/type/size/hash. Extract to `extracted-loader`, never directly into `clean-game`.

- [x] **Step 3: Verify archive tests and digest determinism**

Run the focused test project and assert repeated SHA-256 calculation of a synthetic archive is identical.

### Task 5: Implement transactional loader application and rollback

**Files:**
- Create: `src/ThroneForge.LoaderSmokeTest/LoaderTransactionService.cs`
- Test: `tests/ThroneForge.LoaderSmokeTest.Tests/LoaderTransactionServiceTests.cs`

- [x] **Step 1: Add failing transaction tests**

Test new files, unchanged files, explicit overwrites with backups under the experiment root, created directories, failed apply rollback, and hash restoration after rollback.

- [x] **Step 2: Implement prepare/apply/verify/rollback**

Prepare a manifest before copying. Refuse unexpected files and unsafe destinations, preserve overwritten copy files in an external backup, apply only validated archive entries, verify replacement hashes, and automatically roll back all mutations on failure. Never touch the original game root.

- [x] **Step 3: Verify transaction safety**

Run focused tests and assert failed apply leaves the disposable copy byte-equivalent to its pre-transaction manifest.

### Task 6: Implement bounded launch observation and log sanitization

**Files:**
- Create: `src/ThroneForge.LoaderSmokeTest/LaunchObservationService.cs`
- Create: `src/ThroneForge.LoaderSmokeTest/LoaderLogParser.cs`
- Create: `src/ThroneForge.LoaderSmokeTest/SmokeTestReportWriter.cs`
- Test: `tests/ThroneForge.LoaderSmokeTest.Tests/LaunchObservationTests.cs`
- Test: `tests/ThroneForge.LoaderSmokeTest.Tests/LoaderLogParserTests.cs`
- Test: `tests/ThroneForge.LoaderSmokeTest.Tests/SmokeTestReportWriterTests.cs`

- [x] **Step 1: Add failing launch/parser/report tests**

Test that an executable outside the experiment root is rejected, baseline failure prevents install, graceful close is attempted without `Kill`, synthetic BepInEx logs parse version/preloader/chainloader/config/plugin/error/warning signals, fatal errors classify as `Failed`, absent evidence is `Inconclusive`, and generated reports omit absolute paths/raw logs/binaries.

- [x] **Step 2: Implement bounded launch observation**

Start only an explicit executable under `clean-game`, set its working directory to that root, verify the process path remains inside the experiment, observe for a bounded interval, record sanitized process evidence, and request only graceful window closure. If safe observation or closure is unavailable, return `Inconclusive` and never force-kill.

- [x] **Step 3: Implement sanitized log parsing and atomic report writing**

Parse only the required loader summary fields, remove path/user/machine/stack-trace data, classify `Passed`, `PassedWithWarnings`, `Failed`, or `Inconclusive`, and atomically write the fingerprint-specific Markdown report with no raw log or binary selection.

### Task 7: Add the explicit local harness and orchestration

**Files:**
- Create: `src/ThroneForge.LoaderSmokeTest/SmokeTestOrchestrator.cs`
- Modify: `src/ThroneForge.LoaderSmokeTest/Program.cs`
- Create: `tools/loader-smoke-test/Invoke-ThroneForgeLoaderSmokeTest.ps1`
- Test: `tests/ThroneForge.LoaderSmokeTest.Tests/*.cs`

- [x] **Step 1: Add failing orchestration tests**

Test required arguments, `WhatIf`, baseline-before-install ordering, fingerprint/readiness/indicator gates, original post-verification, private report sanitization, and mandatory rollback/post-check behavior.

- [x] **Step 2: Implement explicit modes**

Support `Plan`, `Prepare`, `Baseline`, `Install`, `Launch`, `Verify`, `Rollback`, and `Full`, with `GamePath`, `ExperimentRoot`, `ExpectedFingerprint`, `BepInExArchivePath`, and dry-run support. Require explicit archive input unless `AllowDownload` is requested. Never scan Steam or infer paths.

- [x] **Step 3: Add the PowerShell wrapper**

Expose the required parameters and pass them to `dotnet run` without embedding personal paths. Validate absolute paths and use an external experiment root. Keep raw archives/logs/manifests outside Git.

### Task 8: Run synthetic validation and the private experiment gates

**Files:**
- Modify: `docs/discovery/README.md`
- Modify: `PLAN.md`
- Modify: `STATUS.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/adr/ADR-0002-target-framework-split-pending-m1-discovery.md`
- Create: `docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-loader-smoke-test.md` only after safe private review

- [x] **Step 1: Run all local synthetic checks before touching the game**

Run restore, format, Release build, focused tests, full tests, architecture tests, contracts tests, and tracked-file hygiene. Do not start the private experiment while any required synthetic check fails.

- [x] **Step 2: Verify official release metadata**

Use `gh api repos/BepInEx/BepInEx/releases/tags/v5.4.23.5`, require owner/repository/tag/asset `BepInEx_win_x64_5.4.23.5.zip`, record asset ID/size/publication/digest where supplied, and store the download only under the external experiment root. If verification or bounded baseline launch is inconclusive, stop before installation and write a sanitized inconclusive report.

- [x] **Step 3: Execute only against the disposable copy**

Recompute original and copied fingerprint/readiness, run the copied baseline first, securely extract the exact archive, apply transactionally, launch only the copied executable, parse sanitized loader evidence, gracefully close, roll back, and recompute the copied/original fingerprints and indicators. Never modify or launch the original installation.

- [x] **Step 4: Record the outcome honestly**

Commit only the sanitized report and documentation. Update ADR-0002 with verified bootstrap compatibility only for `Passed`/`PassedWithWarnings`; keep all plugin TFM, Harmony, lifecycle, game API, and custom-wave claims unverified. For `Failed` or `Inconclusive`, preserve the provisional recommendation and state the blocker.

### Task 9: Hosted verification and handoff

**Files:**
- Modify: `PLAN.md`
- Modify: `STATUS.md`
- Modify: `CHANGELOG.md`

- [x] **Step 1: Run exact local validation available on the host**

Run:

```powershell
dotnet --version
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
git status --short
```

Record any local exact-SDK limitation honestly; do not treat the x86 SDK 10.0.110 workaround as exact SDK validation.

- [x] **Step 2: Push and inspect hosted CI**

Push `agent/m1-loader-smoke-test`, wait for Windows and Ubuntu jobs using SDK `10.0.100`, download every TRX artifact, and record file counts, aggregate totals, failures, skips, and warnings. The real loader experiment must never run in hosted CI.

- [x] **Step 3: Review scope before handoff**

Final hosted evidence: run `30857539959`, commit `634d3e97552d4f9ea619a2b7d9359ffc8bb1cb68`; Windows and Ubuntu each passed with SDK `10.0.100`, 11 TRX files, and 130 tests, with no overwrite warnings. Local format verification was blocked only by the absent exact SDK `10.0.100`; the hosted format checks passed.

Confirm no loader/game binary, archive, raw log, proprietary file, private absolute path, plugin, Harmony reference, game API, lifecycle binding, catalog extraction, or custom-wave code is tracked. Stop with the next task limited to the next evidence-backed M1 investigation.
