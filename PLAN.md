# ThroneForge Implementation Plan

This living plan follows section 25 of `docs/THRONEFORGE_SPEC.md`. Work proceeds one milestone at a time; a milestone is not complete until its required validation passes and `STATUS.md` is updated with exact evidence.

## Current milestone

M0 - Repository bootstrap and architecture skeleton (complete).

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

## First executable task for M1

Create a local-only discovery tool that accepts an explicit Thronefall installation path, detects the managed Mono versus IL2CPP layout and executable architecture, computes a sanitized fingerprint from local metadata, and writes `docs/discovery/<fingerprint>.md` without copying or committing game binaries or assets. Stop after documenting the evidence and blockers; do not guess a lifecycle hook or implement the wave bridge.
