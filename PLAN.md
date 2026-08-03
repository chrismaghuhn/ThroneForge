# ThroneForge Implementation Plan

This living plan follows section 25 of `docs/THRONEFORGE_SPEC.md`. Work proceeds one milestone at a time; a milestone is not complete until its required validation passes and `STATUS.md` is updated with exact evidence.

## Current milestone

M1 - Thronefall discovery spike, task 2 loader and managed-runtime compatibility discovery on `agent/m1-runtime-compatibility`.

## Milestones

### M0 - Repository bootstrap and architecture skeleton

Deliver a clean-clone buildable solution skeleton, pinned external-tool SDK, centralized build/package settings, the stable-core/adapter project boundaries, initial ADRs, architecture tests, CI, and honest project status documentation. Use only placeholder interfaces and portable types; do not reference or name unverified Thronefall internals. Acceptance requires a game-free build, passing architecture tests, no forbidden runtime references outside placeholder game-facing projects, and all required validation commands passing.

### M1 - Thronefall discovery spike

Against an explicit local, legally obtained game path, detect executable architecture, Unity version, Mono/IL2CPP backend, loader compatibility, target framework, and a fingerprint. Document the selected BepInEx/Harmony setup, one verified lifecycle binding, the first binding report, catalog feasibility, and legal/distribution constraints. Do not implement custom waves until the M1 acceptance criteria pass.

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
- [x] Run exact-SDK hosted Windows/Linux CI, inspect both TRX artifacts, and close this task only after the branch-head run is green. Run `30848993321` validated implementation commit `51d30d611af85f41205da5d0f1d7f68514081a58`: Windows and Ubuntu each produced 10 TRX files representing 70 tests with 0 failures/errors and no overwrite warnings.

## Next executable task after M1 discovery task 2

M1 Task 3 is a reversible clean-profile loader smoke test for the selected official candidate. It must be planned only after Task 2 evidence is complete, and it must not be started in the Task-2 branch.

## M1 discovery task 1 hardening checklist

- [x] Reject output roots that are the game root, descendants of it, existing reparse points, or reached through reparse-point parents before creating any directory.
- [x] Normalize output-path and filesystem failures into sanitized `DiscoveryException` messages without absolute paths or stack traces in normal CLI output.
- [x] Select the main executable from a unique `*_Data` base-name match, then root-name match, then exactly one remaining PE executable; report ambiguity as `Unknown`.
- [x] Open selected files once for bounded length validation and hashing; keep deterministic fingerprints.
- [x] Add regression coverage for output protection, renamed installations, crash-handler ordering, ambiguous executables, CLI redaction, and report non-creation after rejected output.
- [x] Correct status, plan, README, and changelog wording; run local and hosted validation without starting loader/runtime work.
