# ThroneForge Implementation Plan

This living plan follows section 25 of `docs/THRONEFORGE_SPEC.md`. Work proceeds one milestone at a time; a milestone is not complete until its required validation passes and `STATUS.md` is updated with exact evidence.

## Current milestone

M1 Task 7 loader-launch investigation is the active repository-only slice on `agent/m1-task7-loader-launch-investigation`, based on merged `main@6252831da5eb363f447b62b0bf37952260249eb2`. It investigates why the final authorized disposable run stopped at `LoaderLaunch` and why the later recovery returned `recovery-runtime-drift`, by preserving bounded launch facts and sanitized relative manifest differences. The old external experiment root is untrusted and will not be opened or reused. No private lifecycle or recovery execution is authorized; M1 Task 8 remains blocked. The detailed plan is [`docs/superpowers/plans/2026-08-05-m1-task7-loader-launch-investigation.md`](docs/superpowers/plans/2026-08-05-m1-task7-loader-launch-investigation.md).

M1 - Task 7 recovery hardening is complete on `agent/m1-lifecycle-binding-smoke-test` from merged `main@9c90657f35406b353b495f0889e1cacf571668e0`. The final authorized lifecycle result remains immutable `Failed` at `LoaderLaunch` with `loader-launch-failed`. The one permitted recovery-only rollback was executed after hosted validation and returned `Failed` with `recovery-runtime-drift`; no retry, lifecycle rerun, or Task 8 work is authorized. The C# `LifecycleExperimentOrchestrator` remains the single owner of stage execution, typed evidence, cleanup/recovery/postchecks, primary-failure persistence, and report generation; PowerShell is only an explicit-input wrapper around the real CLI operations.

PR #7 review correction is active on the same branch. No private lifecycle or recovery execution is permitted in this correction. The work is limited to making plugin-deployed recovery remove the exact owned plugin before rollback drift validation, and propagating typed manual-closure evidence from BaselineLaunch and LoaderLaunch so active profiles never enter cleanup.

The correction also preserves cleanup side-effect results when later rollback work fails and reuses the existing Task-6 state services for recovery. A recovery with no deployed package reports plugin removal as `NotRequired`; a manual-closure result remains `ProcessActive=true` and is persisted for recovery rather than entering cleanup.

The correction head `1cd903c0aa1942d793409893d458939660e40c9d` passed hosted run `31039015040` on Windows and Ubuntu with SDK `10.0.100`; each runner produced 13 TRX files and 360 passing tests with no failure, error, timeout, abort, inconclusive, not-executed or skipped result.

The follow-up review correction is repository-only: recovery keeps `PluginRemovalStatus=NotRequired` when no package was deployed, and BaselineLaunch manual closure records `NotApplied` with an explicit no-loader-cleanup action instead of a misleading rollback command. No private run is authorized.

Correction head `bb960efd9e0715639807469a109b3ce28f700c72` passed PR run `31041425981` and push run `31041429428`; Windows and Ubuntu each reported 13 TRX files and 362 passing tests with all failure, error, timeout, abort, inconclusive, not-executed and skipped counters at zero.

## Milestones

### M0 - Repository bootstrap and architecture skeleton

Deliver a clean-clone buildable solution skeleton, pinned external-tool SDK, centralized build/package settings, the stable-core/adapter project boundaries, initial ADRs, architecture tests, CI, and honest project status documentation. Use only placeholder interfaces and portable types; do not reference or name unverified Thronefall internals. Acceptance requires a game-free build, passing architecture tests, no forbidden runtime references outside placeholder game-facing projects, and all required validation commands passing.

### M1 - Task 6: disposable BepInEx synthetic-plugin smoke test (complete; M1 overall incomplete)

The branch adds an external `ThroneForge.PluginSmokeTest` project, source-only synthetic plugin template, metadata-selected `netstandard2.0`/`netstandard2.1` build path, exact three-file package manifest and digest, immediate Task-4 admission, and a local-only orchestrator that reuses Task-3 copy/transaction/launch/rollback services. The earlier fresh attempt recaptured and metadata-validated the package but failed at disposable-profile deployment-state validation before plugin files were written. After the canonical state-path correction, one fresh private run passed with actual runtime identities, one plugin/nonce marker, successful removal and rollback, complete disposable restoration, and unchanged original verification. M1 Task 7 is now the active bounded follow-up; its private lifecycle run is recorded separately as failed before loader transaction/package deployment.

Task-6 completion is recorded by the sanitized report, unchanged original-installation manifest and runtime post-checks, verified loader rollback, and hosted run `30998768487` for correction head `bda540f9f4c97868d2469c0d5f48826269528e8f`. It does not claim a real Thronefall plugin, lifecycle, game API, Harmony, catalog, or custom-wave capability.

### M1 - Thronefall discovery spike

Against an explicit local, legally obtained game path, detect executable architecture, Unity version, Mono/IL2CPP backend, loader compatibility, target framework, and a fingerprint. Document the selected BepInEx/Harmony setup, one verified lifecycle binding, the first binding report, catalog feasibility, and legal/distribution constraints. Do not implement custom waves until the M1 acceptance criteria pass.

### M1 Task 7 - public Unity lifecycle binding and smoke test (orchestration hardening; M1 incomplete)

- [x] Add metadata-only validation for the public `UnityEngine.Application.quitting` event in `UnityEngine.CoreModule`.
- [x] Add the source-only lifecycle plugin template with exact package identity, synchronous-only lifecycle state machine, and nonce-bound markers.
- [x] Add repository-only lifecycle, marker, log-stability, package, and architecture tests.
- [x] Reuse Task-6 ownership, package capture, admission, deployment, launch, removal, rollback, and post-verification services.
- [x] Run local validation and hosted Windows/Linux synthetic CI before any private run.
- [x] Perform the one permitted fresh corrective private disposable-profile experiment after hosted CI passed; it stopped at `OriginalPreflight` with `original-preflight-failed` before loader transaction/package deployment.
- [x] Commit exactly one sanitized fingerprint-specific lifecycle binding report and run final hosted validation.

Task 7 is limited to the public Unity lifecycle source `UnityEngine.Application.quitting` and does not inspect Thronefall-defined types, gameplay state, Harmony hooks, catalogs, or custom waves.

The correction consumes only `throneforge-runtime-compatibility-evidence-v1`, verifies canonical Task-3/Task-6 state through shared C# services using the saved disposable baseline, reports package capture/admission/deployment as one `AdmitAndDeploy` stage with bounded phase categories, and exercises stage progression and postchecks in repository tests. No private game or BepInEx run is authorized in this correction.

Repository-only correction head `dbbcc3a` passed hosted run `31012843103` on Windows and Ubuntu with SDK `10.0.100`; each runner uploaded 13 TRX files representing 327 tests with zero failures, errors, skips or not-executed tests. No private run was performed.

Task-7 correction baseline: reviewed head `92094c4362f09f73ffdd1bc8807caaf4a904f611` passed hosted run `31005428380` with 13 TRX files and 304 tests per runner using SDK `10.0.100`. Correction head `bdd871fa263d5b97fc4a20160ee110d32f406c99` passed hosted run `31008620029` with 13 TRX files and 313 tests per runner using SDK `10.0.100`; all test counters were zero for failures, errors, skips, not-executed and aborted tests. The first corrective private run stopped at `OriginalPreflight` because the harness expected `Selected executable=...` while the discovery CLI emitted `Selected executable: ...`. The final authorized private run later stopped at `LoaderLaunch` with `loader-launch-failed`; no package, deployment or lifecycle marker was produced. Both results remain immutable historical evidence; no further lifecycle run is permitted.

### Task-7 orchestration ownership correction

- [x] Replace the PowerShell stage machine with the real `run-lifecycle-experiment` C# CLI operation.
- [x] Use `ILifecycleExperimentOperations` and stage-specific evidence; missing evidence must fail closed.
- [x] Persist immutable primary failure stage/category and separate cleanup failures.
- [x] Run cleanup, disposable runtime/readiness/indicator verification, original postchecks, and sanitized report generation in C#.
- [x] Validate the full repository-only implementation and hosted Windows/Linux CI; do not run another private experiment in this slice.

The production-path correction head `39120f1abe4138d3ea0ef6b1842f4a89c66ac079` passed hosted run `31022292522` with 13 TRX files and 339 tests per runner on SDK `10.0.100`; all failure, error, skip, not-executed, aborted and inconclusive counters were zero, and no TRX overwrite warning occurred. No private run was performed; the final documentation head receives a separate hosted validation before this correction is considered ready for authorization.

### Task-7 production-path hardening

- [ ] Create and advance Task-6 ownership state from the real C# workflow.
- [ ] Preserve loader/plugin/process side effects from failed evidence and guard cleanup while a process is active.
- [ ] Add typed manual-closure recovery and an explicit C# rollback operation.
- [ ] Capture the loader-only reference manifest after loader verification and immediately before deployment.
- [ ] Bind package, manifest, Unity metadata, and runtime identity evidence to the owned profile and captured package.
- [x] Add production-path ownership, side-effect, recovery, path-binding and failure coverage without performing a private run.
- [ ] Run local and exact-SDK hosted validation, inspect every TRX artifact, and report readiness for separate private authorization.

Current correction implementation adds a real C#-owned package-build boundary, Task-6 ownership creation before preparation, side-effect-preserving evidence application, typed manual-closure recovery/rollback, owned package and Unity metadata path validation, and independent cleanup/postcheck evidence. The private experiment remains forbidden in this correction. The checklist stays open until the new hosted validation has passed and every TRX artifact has been inspected.

### Task-7 loader-only cleanup and recovery correction

- [x] Add one shared loader-only manifest comparator that permits content changes only for the same recognized BepInEx log path while requiring exact paths, directories, and all nonvolatile hashes.
- [x] Use that comparator in normal plugin removal and C# manual recovery; retain exact disposable-baseline verification after loader rollback.
- [x] Add an executable `Rollback` wrapper mode for the C# `rollback-lifecycle-experiment` operation without reintroducing PowerShell cleanup logic.
- [x] Bind the discovered selected executable to the explicitly supplied executable-relative path and derive the repository baseline commit from `git rev-parse HEAD`.
- [x] Run the repository validation and exact-SDK hosted matrix, inspect every TRX artifact, and report readiness for one separately authorized private verification.

This correction performs repository-only work. Implementation head `2139d41812b5da09c620a1685a217de1caa3510e` passed hosted run `31025440280`; the sanitized final-report head `e0bfd3958d4f2add190877b436737f7a0946b6e9` passed final hosted run `31028231111` on Windows and Ubuntu with SDK `10.0.100`. The recovery correction head `1549552810e826afc415355f87742faaecafc354` passed hosted run `31032572956` with 13 TRX files and 355 tests per runner; all tests passed and every artifact was parsed independently. The one separately authorized final private run was performed once and failed at `LoaderLaunch` with `loader-launch-failed`. The one permitted recovery-only rollback then failed before loader mutation with `recovery-runtime-drift`; no lifecycle binding conclusion is claimed and no retry is authorized.

### Task-7 recovery closure

- [x] Make cleanup conditional on recorded side effects; pre-deployment failure records plugin removal as `NotRequired`.
- [x] Accept only a valid `Failed` ownership record with a bound rollback-eligible transaction for recovery.
- [x] Validate bounded launch-time generated evidence before rollback and preserve exact baseline restoration as the final requirement.
- [x] Keep experiment failure and recovery outcome separate in the sanitized report.
- [x] Execute exactly one recovery-only rollback against the existing failed experiment after green hosted CI; it returned `recovery-runtime-drift` before loader mutation. No retry was performed.

The final authorized experiment remains `Failed` at `LoaderLaunch` with no package, admission, deployment, or lifecycle evidence. Recovery did not establish the lifecycle binding and did not complete rollback; the original-installation postcheck from the experiment remains passed. M1 Task 8 must not begin.

### M2 - Contracts, schemas, validation, and fixtures

Implement portable value objects, manifests, validation issues, schema registry, manifest/wave/configuration schemas, migrations, catalogs, and valid/invalid fixtures. Make Studio, CLI, packaging, runtime, and tests consume one validation facade. Add deterministic serialization and stable error/path coverage.

### M3 - Packaging, installation transactions, profiles, and dependency resolution

Implement constrained `.tforge` reading/building, integrity metadata, archive/path/decompression protections, immutable installation storage, profiles, safe-mode markers, deterministic dependency/conflict resolution, and CLI foundations for inspect/validate/pack/install/list. Prove malicious input rejection, transactional failure behavior, reproducible builds, and deterministic dependency outcomes.

### M4 - Adapter and custom-wave runtime vertical slice

Use the verified M1 profile to implement the adapter binding report, lifecycle source, catalog exporter, wave bridge, content registry, and example custom-wave package. Validate references before activation, handle unsupported adapter capabilities clearly, and complete the documented clean-profile game smoke test.

### M5 - Complete CLI and creator workflow

Implement `tforge new wave`, project validation, build/install, catalog export, doctor, support-friendly JSON output, and stable exit codes. Prove a project can be created and packaged offline against a catalog fixture and that doctor distinguishes game, loader, build, and profile failures.

### M6 - Studio custom-wave MVP

Build the Avalonia shell and shared-service-driven wizard, open/save/recovery flow, catalog browser, timeline editor, properties/problems panels, undo/redo, generated configuration editor, and build/install/launch commands. Run the documented UI smoke-test procedure and prove output compatibility with the CLI source format.

### M7 - In-game manager and diagnostics UX

Add in-game mod/status/details/configuration/compatibility pages, generated configuration controls, explicit full-trust code-mod warning and hash approval, restart-required behavior, support bundles, and safe-mode recovery UI. Test redaction and simulated failed-code-mod recovery.

### M8 - Low-code visual logic

Add versioned graph schemas, typed node registry, graph validation, bounded deterministic interpretation, traces, the Studio node editor, and a small built-in node set. Prove invalid connections/cycles and hostile fixture graphs are rejected and a data-only graph can trigger the existing custom wave without arbitrary code execution.

### M9 - Ecosystem hardening

Evaluate signed metadata, update channels, package sources, richer content, localization tooling, Linux support, public API compatibility, documentation site, and release automation as separately scoped work. M9 is not an MVP commitment.

## M0 execution checklist

- [x] Confirm source-of-truth files and repository state.
- [x] Record the current LTS/toolchain decision and keep game-facing target details pending M1 discovery.
- [x] Add repository-wide ignore/format/build settings and deterministic package pinning.
- [x] Create the solution and all M0 project skeletons with only allowed project references.
- [x] Add portable placeholder contracts and adapter abstractions without invented game internals.
- [x] Add architecture tests before relying on implementation behavior; verify they fail when the skeleton is absent, then pass after bootstrap.
- [x] Add CI for locked restore, formatting, Release build, tests, and architecture tests on Windows and Linux.
- [x] Run every required validation command and repair failures before closing M0.
- [x] Update `STATUS.md` with exact results, risks, and the first M1 task.

## M0 hardening review checklist

This is a bounded follow-up to M0 review; it does not start M1 or inspect the local game installation.

- [x] Pin `global.json` to the exact SDK feature band and make CI install from that file.
- [x] Add `workflow_dispatch`, concurrency cancellation, immutable action references, and OS-specific TRX artifact uploads to CI.
- [x] Keep CI checks ordered as `dotnet --info`, locked restore, format, Release build, and tests without running architecture tests twice.
- [x] Add repository-wide dependency declaration scanning for forbidden game/runtime packages, with synthetic parser tests for forbidden project, central, allowed-test, and harmless-text cases.
- [x] Keep the compiled assembly scan and document the future metadata-only PE inspection follow-up.
- [x] Add minimal `README.md` and contribution/security/license-selection documentation without choosing a license.
- [x] Update `STATUS.md` to distinguish local validation from hosted Windows/Linux CI verification.
- [x] Run the exact local validation and hygiene commands, commit and push this branch, and record the successful hosted Windows/Linux CI run. Open a draft PR only if `main` exists remotely.

## Final M0 TRX artifact fix

This bounded follow-up corrects the solution-level TRX filename collision. It does not start M1 or inspect the local game installation.

- [x] Reproduce the fixed-name TRX overwrite behavior locally.
- [x] Use automatic TRX filenames while keeping one solution-level test execution per runner.
- [x] Verify one TRX file per test project, non-empty counters, zero failures/errors, and aggregate test count before upload.
- [x] Run the corrected workflow on Windows and Linux, inspect both uploaded artifacts, and record the exact run evidence in `STATUS.md` (run `30836315556`, head `0eb597304a1f188c428585e05e014517532fd11c`).

## M1 execution plan: local-only discovery task 1

This task is intentionally limited to synthetic fixtures plus one optional private local run. It does not select BepInEx or Harmony, inspect private members, create bindings, launch the game, or implement runtime integration.

### Repository and architecture preparation

- [x] Confirm the clean `main` baseline and create `agent/m1-discovery` from the verified M0 commit.
- [x] Record in `STATUS.md` that M1 has started, that no discovery conclusion exists yet, and that the host has only SDK `10.0.110` while the repository requires exact SDK `10.0.100`.
- [x] Replace the architecture test's `Assembly.LoadFrom` call with `PEReader` and `MetadataReader`; keep the same forbidden-reference assertions and add `ThroneForge.Discovery` to the explicit source-project allowlist.
- [x] Add the discovery source and test projects to `ThroneForge.slnx` with no game-facing or third-party runtime references.

### Test-first discovery behavior

- [x] Add synthetic fixture tests for Mono, IL2CPP, conflicting, and insufficient layout evidence; verify the expected red failures in an SDK-enabled environment or record the local SDK blocker.
- [x] Add tests for x86, x64, Arm64, and malformed PE headers using generated bytes only.
- [x] Add tests for missing paths, relative paths, file paths, reparse-point escape protection, deterministic fingerprints, changed selected metadata, privacy redaction, atomic writes, and existing-report collision behavior.
- [x] Implement the smallest production slices needed to make those tests pass: explicit path validation, reparse-safe evidence collection, conservative backend classification, metadata-only PE architecture parsing, bounded Unity-version evidence extraction, selected-file hashing, versioned fingerprinting, and atomic Markdown output.
- [x] Add a manual command parser for `inspect --game-path <absolute-path> --output-root <path> [--overwrite]`; return actionable argument/I/O errors without printing the supplied absolute path.

### Documentation and private verification boundary

- [x] Create `docs/discovery/README.md` documenting inputs, non-effects, backend rules, fingerprint v1, privacy guarantees, limitations, and report interpretation.
- [x] Run synthetic tests before any private game inspection. The user-supplied local path was inspected once, the generated report was manually checked for paths/usernames/proprietary details, and only sanitized Markdown was retained.
- [x] The private report passed manual sanitization and is committed; no game binaries or assets were copied.
- [x] `CHANGELOG.md`, `STATUS.md`, and ADR-0002 were reviewed; only verified discovery evidence is documented and M1 remains incomplete.

### Validation and handoff

- [x] Run restore, format, Release build, full tests, architecture tests, contracts tests, and tracked-file hygiene checks; exact pinned-toolchain formatting and hosted build/test evidence passed in CI run `30840490906`.
- [x] Push `agent/m1-discovery`, wait for both hosted CI matrix jobs, and record run IDs, SDK versions, artifact counts, and results.
- [x] Stop with the next M1 investigation explicitly limited to loader/runtime compatibility discovery; do not start the custom-wave vertical slice.

## M1 discovery task 2: loader and managed-runtime compatibility discovery

This task starts from merged `main@e0c46a16fde527dd3a0f99cd5e30f8d5baba571a` on `agent/m1-runtime-compatibility`. It extends the existing external discovery tool and remains limited to bounded local metadata. It must not install or execute a loader, add BepInEx/Harmony/Unity/game references, load assemblies, inspect methods or private game types, implement lifecycle bindings, or start custom waves.

- [x] Add synthetic metadata fixtures and failing tests for netstandard profiles, modern/legacy `mscorlib`, conflicting framework evidence, malformed/oversized candidates, bounded Unity evidence, version-resource evidence, loader indicators, output isolation, deterministic reports, redaction, and architecture boundaries.
- [x] Implement metadata-only managed assembly inspection with `PEReader`/`MetadataReader`, selected framework references, `TargetFrameworkAttribute` decoding, and conservative target-framework classification.
- [x] Implement bounded Unity-version evidence from `UnityVersion.txt`, the beginning of `globalgamemanagers`, and executable/`UnityPlayer.dll` version resources with explicit conflict reporting.
- [x] Implement the fixed-name loader/bootstrap indicator inventory without executing or identifying arbitrary DLLs by filename alone.
- [x] Add `runtime-compatibility --game-path <absolute-path> --fingerprint <sha256> --output-root <path>` and write only `<fingerprint>-runtime-compatibility.md` through the existing safe atomic writer.
- [x] Verify official BepInEx candidate metadata, document the provisional stable BepInEx 5 Unity Mono x64 recommendation, and record uncertainty until a clean-profile smoke test.
- [x] Run synthetic tests before private inspection; generate and manually sanitize the fingerprint-specific runtime report only after tests pass.
- [x] Update discovery documentation, ADR-0002 with a provisional evidence-based recommendation, `STATUS.md`, and `CHANGELOG.md`; keep M1 incomplete.
- [x] Run baseline exact-SDK hosted Windows/Linux CI and inspect both TRX artifacts. Reviewed head `9511e99` passed in run `30849281168`: Windows and Ubuntu each produced 10 TRX files representing 70 tests, including 51 focused Discovery tests, with 0 failures/errors and no overwrite warnings.
- [x] Run the hardening branch-head Windows/Linux matrix and inspect every TRX artifact before closing Task 2; merged result is recorded in main run `30853440786`.

## M1 Task 3: reversible clean-profile loader bootstrap smoke test

Task 2 is complete and merged by PR #2 into protected `main` at `d3f1bb4fde9f77efbb84349f440385cc89002c86`. This task is deliberately separate from the Task-2 branch and evaluates only BepInEx bootstrap initialization for the fingerprint-bound disposable copy. The complete execution plan is [`docs/superpowers/plans/2026-08-03-m1-loader-smoke-test.md`](docs/superpowers/plans/2026-08-03-m1-loader-smoke-test.md).

- [x] Recompute the original installation fingerprint and runtime readiness before preparation.
- [x] Copy the installation without following reparse points and verify the copied fingerprint.
- [x] Run the bounded copied baseline launch before any loader installation.
- [x] Verify and securely extract only the official `BepInEx_win_x64_5.4.23.5.zip` asset.
- [x] Apply the loader transaction only to the disposable copy and retain rollback metadata.
- [x] Launch the copied loader profile, parse sanitized bootstrap evidence, and load no plugin.
- [x] Roll back the disposable copy and verify both original and copied fingerprints/readiness.
- [x] Commit only a sanitized outcome report; do not claim plugin, Harmony, lifecycle, or game API compatibility.
- [x] Run the final hosted Windows/Linux matrix and independently inspect every TRX artifact.

Task-3 historical outcome: BepInEx `5.4.23.5` reported preloader and chainloader initialization with zero plugins and no warnings/errors for fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`. The historical run restored the compatibility fingerprint, but did not retain complete original/disposable manifests; the sanitized report records that limitation. Pre-hardening hosted run `30857962381` on commit `697419a2adba75933f02b4009a16cddf3ff4b0b6` passed on both runners with 11 TRX files and 130 tests per runner using SDK `10.0.100`. M1 remains incomplete.

## M1 Task 3 hardening: fail-closed verification and rollback

- [x] Capture and compare complete original-installation manifests before and after the experiment.
- [x] Re-run original runtime compatibility, readiness, and loader-indicator checks after rollback and feed the structured results into the report.
- [x] Capture and compare complete disposable-copy manifests before launch and after rollback, with fingerprint v1 as an additional check.
- [x] Reject existing `clean-game` directories for fresh `Full` runs and add manifest-backed explicit resume validation.
- [x] Guard every post-apply failure path with deterministic rollback or an explicit manual-closure recovery state.
- [x] Derive and validate the committed report path below `docs/discovery`; remove arbitrary direct CLI report destinations.
- [x] Add regression tests for manifest verification, resume safety, post-apply rollback, recovery markers, and report-path containment.
- [x] Update the Task 3 report and project documentation without fabricating complete historical manifest evidence.
- [x] Run local and hosted validation; do not begin M1 Task 4 until this branch is reviewed and merged.

Task-3 hardening validation: implementation commit `1643cdb4e26f3e5d0890b7b203df8904ec77795c` passed hosted run `30860012681` on Windows and Ubuntu. Each runner used SDK `10.0.100`, uploaded 11 TRX files representing 146 tests, and reported 0 failures, errors, or skips; no TRX overwrite warning occurred. The private experiment was not rerun.

### M1 Task 3 hardening review follow-up

- [x] Derive the original-installation verification report claim from structured pending, passed, failed, and manual-closure states, including failed check categories.
- [x] Require a schema-valid saved baseline for `Baseline`, `Install`, `Launch`, `Verify`, and `Rollback`; only `Prepare` and fresh `Full` may create a baseline.
- [x] Preserve recovery-marker persistence success or failure in guard results, reports, and CLI output without touching an active profile.
- [x] Add regression tests for report evidence states, staged-mode baseline gates, baseline immutability, marker-write failure, and sanitized CLI/report messaging.
- [x] Run the new local validation and hosted Windows/Linux matrix; record the final current-head run before requesting merge.

The review follow-up started from `e904ae5d6f01558f9e4c837505947938b8985630`, whose pre-fix hosted run was `30860548085` with 11 TRX files and 146 tests per runner. Implementation commit `de3159e85c177526c2dafc6b5a60fa80e38c0bc9` passed hosted run `30864308531` on Windows and Ubuntu with SDK `10.0.100`, 11 TRX files, 154 tests, and 0 failures/errors/skips per runner; no overwrite warnings occurred. At that historical checkpoint M1 Task 4 remained unstarted; the private loader experiment was not rerun.

### M1 Task 3 final transaction-state correction

- [x] Replace the unversioned transaction plan with an atomic, schema- and task-versioned transaction state.
- [x] Validate every persisted transaction entry, destination, and backup path again before staged launch, verification, and rollback.
- [x] Verify the complete expected applied profile, replacement hashes, baseline identity, and bounded generated BepInEx evidence before accepting `Applied` or `LaunchObserved`.
- [x] Make staged `Verify` require `LaunchObserved`, expected BepInEx 5.4.23.5 evidence, preloader and chainloader initialization, zero custom plugins, and no fatal loader errors.
- [x] Ensure failed or rolled-back transaction states cannot be used as proof of installation and cannot produce a false-positive staged verification.
- [x] Correct staged launch failure state persistence and the remaining no-transaction report wording.
- [x] Add regression coverage for empty/stale/mismatched transaction state, applied-profile drift, unsafe persisted paths, and bootstrap evidence requirements.
- [x] Run and independently inspect the final hosted Windows/Linux matrix before requesting merge; do not rerun the private experiment.

This correction continues from reviewed head `c98c35bd333f5a82dd008ede7f02ae9217c4140d`. Implementation commit `08940f274d00d6c681c08d36364deedb04474a3d` passed hosted run `30866207996` on Windows and Ubuntu with SDK `10.0.100`; each runner uploaded 11 TRX files representing 165 tests with 0 failures, errors, and skips, and no overwrite warning occurred. At that historical checkpoint M1 Task 4 remained unstarted.

## M1 Task 4: evidence-backed plugin/runtime boundary

Task 3 is merged into `main` by PR #3 at `06554d845a9fe46132c1a19ec0c2f18b8722acf2`. This task is limited to a portable full-trust code-mod boundary and a deterministic pre-load admission gate. It does not load an assembly, select a plugin TFM, add BepInEx/Harmony/Unity references, inspect game methods, implement lifecycle bindings, export a catalog, or implement custom waves. The detailed execution plan is [`docs/superpowers/plans/2026-08-04-m1-plugin-runtime-boundary.md`](docs/superpowers/plans/2026-08-04-m1-plugin-runtime-boundary.md).

- [x] Record the explicit trust and activation-boundary ADR.
- [x] Add immutable portable code-mod identity, integrity, and approval request contracts.
- [x] Add the public lifecycle contract without invented event payloads or game types.
- [x] Add a runtime admission gate that requires verified package integrity, supported adapter compatibility, and explicit user approval; do not load or invoke code.
- [x] Add regression and architecture tests for the boundary and forbidden dependencies.
- [x] Run exact local/hosted validation and update `STATUS.md`; keep M1 incomplete and defer actual plugin/runtime integration.

Task-4 bounded-slice validation: final documentation head `5d7c69a66dd431da98e16e6d2e9ea154728d8387` passed hosted run `30868670093` on Windows and Ubuntu with SDK `10.0.100`. Each runner uploaded 11 TRX files representing 175 tests with 0 failures, errors, or skips; no TRX overwrite warning occurred. The implementation head `c680083` had the same green result in run `30868441588`. Local SDK `10.0.110` also passed locked restore, Release build with 0 warnings/errors, and all 175 tests; exact local formatting remained unavailable because SDK `10.0.100` is not installed. Task 4 and its hardening are now merged by PR #4 at `main@5f4b4dd`; final head validation was run `30878236039` with 11 TRX files and 212 tests per runner. M1 remains incomplete: no Thronefall plugin assembly has been loaded, and plugin TFM, Harmony compatibility, lifecycle bindings, game APIs, catalog extraction, and custom waves remain unverified.

## M1 Task 5: repository-only synthetic plugin-load probe

Task 4 and its evidence-binding hardening are complete and merged into `main` by PR #4 at `5f4b4dd0714d0cffaf9f3267b6f0651ecf6e043e`. This task is deliberately narrower than a real Thronefall plugin experiment. It adds an external probe that captures and hashes one explicit synthetic primary assembly, performs metadata-only single-assembly closure preflight, rebuilds verified integrity evidence, re-runs the Task-4 admission gate immediately before loading, and loads one public closed test-only `IThroneForgeMod` implementation into a collectible .NET `AssemblyLoadContext`. It explicitly invokes no plugin constructor or ThroneForge lifecycle method; assembly loading remains full-trust.

- [x] Add the external `ThroneForge.PluginLoadTest` project and a source-only synthetic plugin fixture.
- [x] Reuse the existing admission gate and bind the exact artifact hash, canonical mod identity, game fingerprint, adapter evidence, and approval before loading.
- [x] Load only the synthetic fixture in a collectible context; record sanitized assembly/type identities and never invoke the plugin.
- [x] Add fail-closed tests for hash, approval, compatibility, malformed assembly, contract-shape, unload, sanitization, and architecture boundaries.
- [x] Run exact hosted Windows/Linux validation; do not run a private Thronefall or BepInEx plugin experiment in this task.

Task-5 final hosted validation: run `30880625516` tested head `15127329aa0fd916438a0404ba51a8dfdfbf59eb`. Windows and Ubuntu each used SDK `10.0.100`, uploaded 12 TRX files representing 226 tests, and reported 0 failures, 0 errors, and 0 skipped tests. The first CI attempt `30880510322` exposed that fixture projects were incorrectly counted as test projects; the workflow now derives expected TRX projects from `IsTestProject=true`. No overwrite warning occurred in the final run. No private loader or game experiment was run.

The detailed design is [`docs/superpowers/specs/2026-08-04-m1-plugin-load-smoke-test-design.md`](docs/superpowers/specs/2026-08-04-m1-plugin-load-smoke-test-design.md), and the execution plan is [`docs/superpowers/plans/2026-08-04-m1-plugin-load-smoke-test.md`](docs/superpowers/plans/2026-08-04-m1-plugin-load-smoke-test.md). The next separate experiment, if approved, must use an explicit disposable profile and remain distinct from game lifecycle/API compatibility. Task-5 hashes only the primary assembly; multi-file package closure integrity is not designed.

### M1 Task 5 hardening: single-assembly closure and unload evidence

This follow-up starts from reviewed head `10897f7b03d45f1e470b5930a9dc1341939cde6` on `agent/m1-plugin-load-smoke-test-hardening`. It remains repository-only: no private Thronefall/BepInEx experiment, plugin package, loader binary, game reference, lifecycle invocation, or game API inspection is permitted. The detailed plan is [`docs/superpowers/plans/2026-08-04-m1-plugin-load-smoke-test-hardening.md`](docs/superpowers/plans/2026-08-04-m1-plugin-load-smoke-test-hardening.md).

- [x] Replace arbitrary sidecar resolution with metadata-only single-assembly closure validation and exact shared API/Contracts resolution.
- [x] Reject native imports and module initializers before `LoadFromStream`; describe assembly loading as full-trust.
- [x] Require exactly one public, top-level, non-abstract, closed `IThroneForgeMod` implementation.
- [x] Preserve one captured byte buffer for size validation, hashing, admission, and loading.
- [x] Require bounded collectible-context unload observation and test retained-reference failure reporting.
- [x] Add source-only helper/native/module-initializer/invalid-shape fixtures and sanitized regression tests.
- [x] Update documentation after the branch-head hosted run and keep M1 overall incomplete.

Task-5 hardening hosted validation: run `30895225156` tested head `bb32de1dd211ddd5174f8d8f6e6490164da970db`. Windows and Ubuntu each used SDK `10.0.100`, uploaded 12 TRX files representing 239 tests, and reported 0 failures, 0 errors, and 0 skipped tests. Both artifacts were independently parsed; no `Overwriting results file` warning occurred. Run `30895090026` was superseded after a formatter import-order failure and is retained only as diagnostic history. No private loader or game experiment was run.

### M1 Task 5 final native-image and load-context correction

This follow-up continues from documentation head `1649336681fd76cb1f66623d151179015c812fcb` and pre-fix hosted run `30895740658`. It remains repository-only and must reject non-IL-only, native-entry-point, missing-CLR-header, and wrong-context images before a successful result.

- [x] Add temporary-PE mutation tests for missing `ILOnly`, present `NativeEntryPoint`, and missing CLR header.
- [x] Add structured pure-managed image evidence and actual owning-context classification.
- [x] Require `CorFlags.ILOnly`, reject `CorFlags.NativeEntryPoint`, and reject missing CLR headers before load.
- [x] Verify `AssemblyLoadContext.GetLoadContext(assembly)` is the expected collectible context.
- [x] Preserve path sanitization, unload observation, and architecture boundaries.
- [x] Run the new exact-SDK hosted matrix, inspect every TRX artifact, and update this plan with the final current-head run.

Final Task-5 native-image/load-context validation: run `30978672030` tested head `a8595e7b56b80ab338e5d148c1ba1f13455598d4`. Windows and Ubuntu each used SDK `10.0.100`, uploaded 12 TRX files representing 244 tests, and reported 0 failures, 0 errors, and 0 skipped tests. Both artifacts were independently parsed; no `Overwriting results file` warning occurred. No private loader or game experiment was run.

### M1 Task 4 hardening: bound trust evidence and admission artifacts

This historical follow-up started from reviewed head `18f7b1135b6aaba04290198f355e6e9ac6a97b5d` on `agent/m1-plugin-runtime-boundary-hardening`. It was a data-only boundary task: no plugin was loaded, no assembly was inspected, and no loader or game dependency was introduced.

- [x] Add canonical, bounded mod identity and version value rules; reject control characters, whitespace, path/device syntax, empty ID components, and invalid version text.
- [x] Replace integrity and approval booleans with immutable records bound to the canonical mod identity, exact package SHA-256, and exact game fingerprint.
- [x] Replace naked adapter compatibility with canonical, fingerprint-bound compatibility evidence and fail closed for missing, warning, conflicting, or unknown states.
- [x] Add an immutable admission binding and deterministic UTF-8/SHA-256 binding digest to every decision with sufficient evidence.
- [x] Enforce deterministic fail-closed gate precedence and stable reason-code constants without assembly loading, reflection, process launch, or game access.
- [x] Add regression tests for identity canonicalization, cross-record mismatches, digest changes, unknown states, path-free records, and existing architecture boundaries.
- [x] Update ADR-0005, ADR-0006, README, STATUS, CHANGELOG, and the detailed Task-4 plan to describe evidence binding as trusted input rather than a cryptographic signature or OS sandbox.
- [x] Run local validation and exact-SDK hosted Windows/Linux validation; inspect every TRX artifact before requesting merge. M1 remains incomplete.

Implementation validation: commit `382fe877f5685bb71d77ee6b5cf04afc8a57a7bf` passed hosted run `30878049044` on Windows and Ubuntu with SDK `10.0.100`. Each runner uploaded 11 TRX files representing 212 tests with 0 failures, errors, or skips; no TRX overwrite warning occurred. Both artifacts were downloaded and parsed independently.

## M1 discovery task 2 hardening (completed and merged)

This completed follow-up closed review findings without installing or executing a loader. It preserved the existing fingerprint-v1 inputs.

- [x] Refactor Task 1 and Task 2 to consume one non-writing installation snapshot and fingerprint service.
- [x] Recompute fingerprint v1 before runtime report generation and reject mismatched supplied fingerprints without creating output.
- [x] Add regression coverage for matching, mismatched, changed-file, changed-backend, uppercase, deterministic, and no-write fingerprint cases.
- [x] Separate provisional loader-candidate selection from structured clean-profile smoke-test readiness.
- [x] Block readiness for every non-absent loader/bootstrap indicator, conflicting evidence, unsupported backend, unknown architecture, or unusable framework evidence.
- [x] Replace generic TFM confidence prose with an evidence-specific assessment and confidence level.
- [x] Categorize conflict, missing evidence, bounded-inspection limitations, and warnings independently in the runtime report.
- [x] Correct the fingerprint-specific report and project documentation after the branch-head hosted CI run passed.
- [x] Inspect every hosted TRX artifact and record the exact current-head evidence; leave M1 Task 3 unstarted.

Final hardening validation: run `30852340193` tested commit `09850c955eff216264ad23a84b035c1057e8bcca`. Windows and Ubuntu each passed with SDK `10.0.100`, 10 TRX files, 99 represented tests, 0 failures/errors, and 0 skipped tests. No overwrite warning occurred.

Task-2 merge evidence: PR #2 merged into `main` at `d3f1bb4fde9f77efbb84349f440385cc89002c86`; main run `30853440786` passed on Windows and Ubuntu with 10 TRX files and 99 tests per runner.

## M1 discovery task 1 hardening checklist

- [x] Reject output roots that are the game root, descendants of it, existing reparse points, or reached through reparse-point parents before creating any directory.
- [x] Normalize output-path and filesystem failures into sanitized `DiscoveryException` messages without absolute paths or stack traces in normal CLI output.
- [x] Select the main executable from a unique `*_Data` base-name match, then root-name match, then exactly one remaining PE executable; report ambiguity as `Unknown`.
- [x] Open selected files once for bounded length validation and hashing; keep deterministic fingerprints.
- [x] Add regression coverage for output protection, renamed installations, crash-handler ordering, ambiguous executables, CLI redaction, and report non-creation after rejected output.
- [x] Correct status, plan, README, and changelog wording; run local and hosted validation without starting loader/runtime work.

## M1 Task 6 hardening follow-up (state-path correction complete)

- [x] Add an atomically persisted, fingerprint-bound Task-6 ownership record and reject unowned rollback/cleanup targets.
- [x] Derive plugin-deployment preconditions from the owned disposable profile, loader transaction state, complete current manifest, process state, and empty custom-plugin root.
- [x] Recapture the exact three package files once, validate their metadata and actual target framework, rerun admission, and retain the same captured bytes for deployment.
- [x] Make plugin deployment transactional and remove partial files/directories on every write or final-manifest failure.
- [x] Enforce the exact synthetic package shape, IL-only/no-native/no-PInvoke/no-module-initializer rules, exact identities, BepInEx metadata, and one public plugin implementation.
- [x] Emit runtime API/Contracts identities from the loaded assemblies instead of build-time constants; add metadata-only net10.0/netstandard2.1 public-surface parity checks.
- [x] Add structured recovery-marker persistence and malformed-marker handling; do not claim recovery when the marker is unavailable.
- [x] Run local validation with installed SDK 10.0.110, restore `global.json` to committed SDK 10.0.100, push the hardened branch, and inspect green hosted Windows/Linux CI with complete TRX artifact inspection.
- [x] Perform the required fresh private attempt after hosted CI in an external profile. The corrected run passed with exact package recapture/admission, actual runtime identities, one plugin marker, successful plugin removal, loader rollback, complete disposable restoration, and unchanged original verification.
- [x] Centralize the canonical Task-3 baseline and transaction-state paths and use them from both the loader orchestrator and Task-6 deployment service.
- [x] Bind Task-6 loader-state validation to the saved disposable baseline manifest and expose stable sanitized state-gate failure categories.
- [x] Add positive real-service context-derivation coverage plus negative legacy/missing/mismatch state tests.
- [x] Perform exactly one fresh private follow-up only after exact-SDK hosted synthetic CI passes; hosted run `30998768487` passed with 13 TRX files and 279 tests per runner.
- [x] Keep M1 Task 7 out of scope for the Task-6 implementation.
