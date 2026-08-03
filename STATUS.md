# STATUS

## Current milestone

M1 - Thronefall discovery spike, task 2 loader and managed-runtime compatibility discovery on `agent/m1-runtime-compatibility`

## State

M0 and M1 discovery task 1 are complete and merged into protected `main` at `e0c46a16fde527dd3a0f99cd5e30f8d5baba571a`. M1 task 2 implementation and private evidence generation are complete locally on `agent/m1-runtime-compatibility`; hosted branch-head verification is pending. M1 overall remains incomplete.

## Completed

- Read `AGENTS.md` and `docs/THRONEFORGE_SPEC.md` completely.
- Inspected the repository and confirmed it has no commits or existing solution/projects.
- Identified a local Thronefall installation as untracked discovery input; it is excluded by `.gitignore` and is not a source or release artifact.
- Created the milestone roadmap in `PLAN.md`.
- Confirmed the specification requires five initial ADRs and architecture tests before game-specific work.
- Added the pinned .NET 10 external-tool configuration, deterministic build properties, central package versions, package lock files, formatting rules, and private-installation ignores.
- Added `ThroneForge.slnx` with all declared M0 source and test project boundaries.
- Added only portable placeholder contracts and the adapter abstraction interfaces from the specification; no game internals or game-facing dependencies were selected.
- Added project-reference and compiled-assembly architecture tests, plus one discoverability smoke test per future test project.
- Added Windows/Linux GitHub Actions CI for locked restore, format verification, Release build, full tests, and architecture tests.
- Started `agent/m0-hardening` from reviewed commit `f7f3114`; M1 has not started.
- Pinned SDK selection to .NET `10.0.100` with `rollForward: disable`; CI reads the repository `global.json`.
- Pinned current GitHub-maintained actions to immutable commits: checkout `v7.0.1` (`3d3c42e5aac5ba805825da76410c181273ba90b1`), setup-dotnet `v6.0.0` (`a98b56852c35b8e3190ac28c8c2271da59106c68`), and upload-artifact `v7.0.1` (`043fb46d1a93c77aae656e7c1c64a875d1fc6a0a`).
- Added CI manual dispatch, branch/PR concurrency cancellation, OS-specific TRX output, and always-run test-result artifact uploads.
- Fixed solution-level TRX filename collisions and added a pre-upload completeness check for one result file per test project.
- Added repository-wide dependency declaration scanning for central props, source projects, and source lock files, with synthetic forbidden/allowed/harmless-input tests.
- Added `README.md`, `SECURITY.md`, and `CONTRIBUTING.md` without selecting a license.
- Verified in GitHub that `main` is the default branch and that the active `Protect main` ruleset targets `main`, requires pull requests and `Validate (windows-latest)`/`Validate (ubuntu-latest)`, blocks branch deletion, and blocks force pushes. No second approval is required.
- Created `agent/m1-discovery` from clean `main@37c50febfe0ab32231000c194d7f2f853463148c`.
- Replaced architecture-test `Assembly.LoadFrom` inspection with metadata-only `PEReader`/`MetadataReader` inspection before any target-framework divergence.
- Added the portable `ThroneForge.Discovery` external tool and its solution/test project, with no project references or game-facing packages.
- Added synthetic discovery fixtures for Mono, IL2CPP, conflicting/unknown layouts, PE architecture, path safety, reparse-point handling, deterministic fingerprints, sanitization, atomic writes, and report collisions.
- Implemented conservative backend evidence collection, x86/x64/Arm64 PE header parsing, bounded Unity-version evidence, selected-file SHA-256 hashing, fingerprint v1, atomic Markdown reports, and the `inspect` command.
- Added `docs/discovery/README.md` and generated the sanitized private report `docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d.md`.
- Verified discovery evidence for this fingerprint: backend `Mono`, executable architecture `X64`, Unity version `Unknown`, fingerprint algorithm `throneforge-game-fingerprint-v1`, fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`.
- Confirmed final current-head CI run `30840490906` on Windows and Ubuntu: 10 TRX files, 35 tests, 0 failures/errors per runner, SDK `10.0.100`.
- Merged PR #1 (`agent/m1-discovery-hardening` into `main`) after required Windows/Linux checks passed; merge commit `e0c46a16fde527dd3a0f99cd5e30f8d5baba571a`.
- Created and pushed `agent/m1-runtime-compatibility` from the merged `main` baseline.
- Added the Task 2 `runtime-compatibility` command, metadata-only managed assembly inspection, bounded Unity-version evidence, runtime-layout evidence, loader/bootstrap indicator inventory, conservative target-framework classification, and atomic sanitized report generation.
- Added synthetic Task 2 coverage for netstandard and `mscorlib` profiles, conflicting and malformed evidence, bounded Unity metadata, version-resource normalization, loader indicators, output isolation, deterministic reports, collision behavior, CLI redaction, and runtime-layout evidence.
- Verified official BepInEx release metadata and documentation on 2026-08-03. The report compares BepInEx 5.4.23.5 stable/LTS with BepInEx 6.0.0-pre.2 pre-release and keeps the BepInEx 5 Unity Mono x64 choice provisional.
- Ran the private Task 2 command against the explicitly supplied local installation without modifying it. The sanitized report is committed at `docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-runtime-compatibility.md`.
- Private Task 2 evidence for this fingerprint: managed runtime `Mono`; executable `X64`; Unity `2022.3.62f2` from bounded `globalgamemanagers` evidence, with matching version-resource build-number evidence; `netstandard.dll` plus Unity 2022.3 evidence supports a provisional `netstandard2.1` candidate; no BepInEx, Doorstop, MelonLoader, Mods, Plugins, `winhttp.dll`, or `version.dll` indicators were present.

## Validation

All commands below were run with the pinned .NET SDK 10.0.100 provisioned outside the repository because the host did not expose `dotnet` on PATH.

- `dotnet --version`: PASS; `10.0.100`.
- `dotnet restore --locked-mode`: PASS; all projects restored.
- `dotnet build -c Release --no-restore`: PASS; 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: PASS; 19 tests passed, 0 failed, 0 skipped, including 11 architecture tests.
- `dotnet format --verify-no-changes --no-restore`: PASS; no output, exit code 0.
- `dotnet test tests/ThroneForge.Contracts.Tests -c Release --no-build`: PASS; 1 test passed, 0 failed.
- Tracked binary-like file scan: PASS; no `.dll`, `.exe`, `.so`, `.dylib`, `.pdb`, `.assets`, or `.bundle` paths tracked.
- Tracked copied-game-directory scan: PASS; no `Thronefall/` or `lib/game/` paths tracked.

The red-green architecture-test check was also performed: the initial test run failed on the intentionally absent M0 skeleton, and the post-bootstrap Release run passed all five architecture tests.

M1 local validation used the installed x86 .NET SDK `10.0.110` from the parent directory so the repository `global.json` was not selected. This is compile/test feedback only; canonical repository commands still require exact SDK `10.0.100`.

- `dotnet restore --force-evaluate` from the parent directory: PASS with SDK `10.0.110`; regenerated the new test project's lockfile for its project reference.
- `dotnet build SDK\ThroneForge.slnx -c Release --no-restore`: PASS; 0 warnings, 0 errors.
- Focused `ThroneForge.Discovery.Tests`: PASS; 16 passed, 0 failed, 0 skipped.
- Full `dotnet test SDK\ThroneForge.slnx -c Release --no-build`: PASS; 35 passed, 0 failed, 0 skipped, including 11 architecture tests.
- `dotnet format SDK\ThroneForge.slnx --verify-no-changes --no-restore`: BLOCKED because the formatter's build host resolves repository `global.json` and exact SDK `10.0.100` is not installed locally.
- Private command, with the absolute game path redacted here: `dotnet run --project src/ThroneForge.Discovery --no-restore -- inspect --game-path <redacted> --output-root docs/discovery`: PASS.
- Private report review: PASS; backend `Mono`, executable architecture `X64`, Unity version `Unknown`, fingerprint algorithm `throneforge-game-fingerprint-v1`, fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`.
- Private report sanitization: PASS; no absolute path, username, machine name, parent traversal, arbitrary listing, or temporary report file was present.
- Final hosted CI passed in GitHub Actions run [30840265844](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30840265844) for branch head commit `233ee8e8e5ac9ddc2f2828a2228447a198601c63`:
  - `ubuntu-latest`: PASS; job `91775313489`; SDK `10.0.100`; 10 TRX files representing 35 tests and 0 failed/errors; artifact `throneforge-test-results-ubuntu-latest-30840265844` (artifact `8866496498`).
  - `windows-latest`: PASS; job `91775313585`; SDK `10.0.100`; 10 TRX files representing 35 tests and 0 failed/errors; artifact `throneforge-test-results-windows-latest-30840265844` (artifact `8866517471`).
- Hosted logs and both downloaded artifact directories were independently checked; no `Overwriting results file` warning occurred.

### M1 discovery hardening validation

The hardening implementation is complete and hosted-verified on `agent/m1-discovery-hardening`; M1 task 1 is merged, and M1 task 2 is being developed separately on `agent/m1-runtime-compatibility`.

- Output roots are validated before any output directory is created: the game root, descendants, existing reparse points, and reparse-point parents are rejected. Filesystem failures are converted to sanitized `DiscoveryException` messages.
- Main-executable selection is deterministic: unique top-level `*_Data` base-name match, then installation-directory-name match, then exactly one valid top-level PE executable; multiple candidates remain ambiguous and produce architecture `Unknown`.
- Selected compatibility files and Unity-version evidence use bounded single-open reads where applicable; fingerprints remain deterministic.
- New regression coverage passes locally: output protection, sibling-path handling, reparse points, renamed installations, crash-handler ordering, ambiguous/unique executables, CLI redaction, deterministic fingerprints, and report non-creation.
- Local full solution validation with installed SDK `10.0.110` from the parent directory: restore PASS, Release build PASS (0 warnings/0 errors), full tests PASS (46 passed, 0 failed, 0 skipped), architecture tests PASS (11), and Contracts tests PASS (1).
- Local `dotnet format --verify-no-changes --no-restore` could not run because the formatter's build host resolves the repository's exact SDK requirement `10.0.100`, which is not installed on this machine. The exact pinned format/build/test path remains subject to hosted CI.
- Private report re-run passed with the sanitized evidence unchanged: `Mono`, `X64`, Unity `Unknown`, fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`, and no absolute path, username, machine name, or temporary file.

Hosted verification passed in GitHub Actions run [30844364366](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30844364366) for final hardening commit `d1ddc2bcec991fa1f12016c4ad5712557a1f8fa4`:

- `windows-latest`: PASS; job `91788915294`; SDK `10.0.100`; 10 TRX files representing 46 tests, 0 failed, 0 errors; artifact `throneforge-test-results-windows-latest-30844364366` (artifact `8868100813`).
- `ubuntu-latest`: PASS; job `91788915163`; SDK `10.0.100`; 10 TRX files representing 46 tests, 0 failed, 0 errors; artifact `throneforge-test-results-ubuntu-latest-30844364366` (artifact `8868078879`).

Both jobs completed locked restore, exact-SDK information, format verification, Release build, full tests, TRX completeness verification, and upload. Both downloaded artifacts were independently parsed. No `Overwriting results file` warning occurred in either log, and each artifact contains one TRX per test project. No local game installation was available to CI.

### M1 discovery task 2 local validation

- Focused `ThroneForge.Discovery` build: PASS with installed SDK `10.0.110` from the parent directory; 0 warnings and 0 errors.
- Focused `ThroneForge.Discovery.Tests`: PASS; 49 tests passed, 0 failed, 0 skipped.
- Private command, with the absolute game path redacted here: `dotnet run --project src/ThroneForge.Discovery --no-restore -- runtime-compatibility --game-path <redacted> --fingerprint 1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d --output-root docs/discovery --overwrite`: PASS.
- Private report review: PASS; no absolute path, username, machine name, arbitrary listing, binary content, decompiled source, or temporary report remained. The report contains only relative paths and selected compatibility metadata.
- Hosted branch-head CI: pending. No hosted Task 2 result is claimed until both required matrix jobs and TRX artifacts complete successfully.

## Unverified assumptions

- Backend `Mono`, executable architecture `X64`, and fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d` are verified only for the documented local installation fingerprint.
- The Task 1 Unity `Unknown` result was superseded for this same fingerprint by Task 2's additional bounded `globalgamemanagers` and version-resource evidence: Unity `2022.3.62f2` is locally evidenced, but not generalized to other installations.
- The Task 2 target-framework recommendation is provisional `netstandard2.1 candidate`; exact plugin compatibility remains unverified. Loader, Harmony compatibility, private members, lifecycle hooks, game APIs, and wave representation remain unknown.
- The M0 external-tool target is planned as `net10.0` because the specification names .NET 10 as the active LTS at its research checkpoint; the game-facing target remains provisional until M1 evidence.
- No game-facing behavior is implemented or claimed.

## Risks and blockers

- The host's default PATH still does not contain `dotnet`; the only directly available SDK is x86 `10.0.110`, while CI and maintainers need exact `10.0.100` for canonical validation.
- The local game directory contains proprietary files and must remain ignored and outside Git history.
- The M0 baseline is committed; future changes must continue to exclude the local game directory.
- Exact SDK `10.0.100` local formatting/validation remains unverified; hosted CI is required for that pinned-toolchain result.
- The private reports record local layout evidence: Mono indicators were found under `MonoBleedingEdge` and `thronefall_Data/Managed`, plus `Assembly-CSharp.dll`; Task 2 adds bounded Unity, managed-framework, and loader-indicator evidence. All conclusions remain installation-specific.
- Task 2 remains metadata-only and bounded. The BepInEx 5.4.23.5 Unity Mono x64 candidate is a provisional recommendation only; no loader was downloaded, installed, or executed. A clean-profile smoke test is still required.
- Hosted Task 2 validation and artifact inspection are still outstanding for the current branch head.

## Next task

Push the reviewed Task 2 implementation, wait for the hosted Windows/Linux CI matrix, inspect both TRX artifacts, and then merge only after the branch-head checks pass. The next M1 task is a reversible clean-profile loader smoke test for the provisional BepInEx 5 candidate; do not start it in this branch.
