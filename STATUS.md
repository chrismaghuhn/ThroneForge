# STATUS

## Current milestone

M1 - Task 6 disposable BepInEx synthetic-plugin smoke test is complete on `agent/m1-disposable-bepinex-plugin-smoke-test` from merged `main@f6416874c0ca1c0750407a126e515bdefcf84563`; M1 Task 7 has not started.

## State

M0 and M1 discovery tasks 1 and 2 are complete and merged into protected `main`. M1 task 3 and its reusable harness hardening are complete and were merged by PR #3 at `06554d845a9fe46132c1a19ec0c2f18b8722acf2`; the private experiment was not rerun during hardening. M1 Task 4 and its evidence-binding hardening are complete and were merged by PR #4 at `5f4b4dd0714d0cffaf9f3267b6f0651ecf6e043e`. The final Task-4 head validation was run `30878236039` with SDK `10.0.100`, 11 TRX files, and 212 tests per runner. M1 Task 5 repository-only hardening is complete and hosted-verified below. M1 Task 6 has completed one private disposable synthetic-plugin run; M1 overall remains incomplete.

The Task 4 bounded slice and hardening passed hosted validation before PR #4 merged: run `30878236039` used SDK `10.0.100` on Windows and Ubuntu, with 11 TRX files and 212 tests per runner, all passing. Task 5 final hosted validation is recorded below. Task 6's private result is recorded below; current-head hosted validation remains pending until the documentation and harness fix are pushed.

Task 6 implementation is complete. The repository-only surface includes the external plugin-smoke project, evidence-driven TFM selection, metadata-only PE inspection, a versioned three-file package manifest/digest, Task-4 admission binding, nonce marker/log parsing, deployment guards, and a PowerShell harness that reuses the Task-3 disposable profile services. The private run passed in an external disposable profile; no private artifact was copied into the repository.

The next executable gate is current-head hosted Windows/Linux CI with SDK `10.0.100`, followed by review of the Task-6 report and merge decision. M1 Task 7 remains unstarted.

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
- Focused `ThroneForge.Discovery.Tests` on the pre-hardening implementation: PASS; 49 tests passed, 0 failed, 0 skipped. The reviewed head `9511e99` subsequently represented 51 focused Discovery tests in hosted CI.
- Private command, with the absolute game path redacted here: `dotnet run --project src/ThroneForge.Discovery --no-restore -- runtime-compatibility --game-path <redacted> --fingerprint 1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d --output-root docs/discovery --overwrite`: PASS.
- Private report review: PASS; no absolute path, username, machine name, arbitrary listing, binary content, decompiled source, or temporary report remained. The report contains only relative paths and selected compatibility metadata.
- Baseline hosted Task 2 evidence before hardening: run [30849281168](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30849281168) validated reviewed head `9511e999226c572115762a83d3baf3211c75d9d9`; Windows and Ubuntu each produced 10 TRX files representing 70 tests, including 51 focused Discovery tests, with 0 failed/errors and exact SDK `10.0.100`. Both uploaded artifacts were downloaded and parsed independently, and no `Overwriting results file` warning occurred. This is not the final hardening validation.

## Unverified assumptions

- Backend `Mono`, executable architecture `X64`, and fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d` are verified only for the documented local installation fingerprint.
- The Task 1 Unity `Unknown` result was superseded for this same fingerprint by Task 2's additional bounded `globalgamemanagers` and version-resource evidence: Unity `2022.3.62f2` is locally evidenced, but not generalized to other installations.
- The Task 2 target-framework recommendation is provisional `netstandard2.1 candidate`; exact plugin compatibility remains unverified. Loader, Harmony compatibility, private members, lifecycle hooks, game APIs, and wave representation remain unknown.
- The current fingerprint-specific assessment is `Medium` confidence on the basis of a netstandard compatibility surface plus Unity 2022.3 evidence; it remains provisional and does not authorize a loader test.
- The M0 external-tool target is planned as `net10.0` because the specification names .NET 10 as the active LTS at its research checkpoint; the game-facing target remains provisional until M1 evidence.
- No game-facing behavior is implemented or claimed.

## Risks and blockers

- The host's default PATH still does not contain `dotnet`; the only directly available SDK is x86 `10.0.110`, while CI and maintainers need exact `10.0.100` for canonical validation.
- The local game directory contains proprietary files and must remain ignored and outside Git history.
- The M0 baseline is committed; future changes must continue to exclude the local game directory.
- Exact SDK `10.0.100` local formatting/validation remains unverified; hosted CI is required for that pinned-toolchain result.
- The private reports record local layout evidence: Mono indicators were found under `MonoBleedingEdge` and `thronefall_Data/Managed`, plus `Assembly-CSharp.dll`; Task 2 adds bounded Unity, managed-framework, and loader-indicator evidence. All conclusions remain installation-specific.
- Task 2 remains metadata-only and bounded. Task 3 verified only BepInEx bootstrap for the exact fingerprint; plugin target-framework compatibility, Harmony compatibility, lifecycle hooks, game APIs, catalog extraction, and custom waves remain unverified.
- The private Task 3 result is complete, but exact-SDK hosted CI for the new synthetic tests and architecture boundaries is still required before closing the branch.

### M1 discovery task 2 hardening status

- Fingerprint recomputation, clean-profile readiness separation, evidence-specific TFM confidence, and independent evidence categories are complete on `agent/m1-runtime-compatibility-hardening`.
- Local hardening build/test validation with the installed parent-directory SDK `10.0.110`: Release build PASS with 0 warnings/errors; full solution tests PASS with 99 tests, 0 failed, 0 skipped; focused Discovery tests PASS with 80 tests, 0 failed, 0 skipped; Architecture tests PASS with 11 tests and Contracts tests PASS with 1 test.
- Local `dotnet format SDK\ThroneForge.slnx --verify-no-changes --no-restore` remains blocked because the host lacks the repository-pinned SDK `10.0.100`; hosted CI is required for the exact-toolchain formatting result.
- Private hardening re-run, with the absolute game path redacted here, passed against the documented fingerprint: `Mono`, `X64`, `Netstandard21Candidate`, confidence `Medium`, readiness `ReadyForReversibleTest`; the report contains no absolute path or private system information.
- Final hosted validation passed in run [30852764951](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30852764951) for commit `456360480c265e29817aa6c818274341cdbced54`:
  - `windows-latest`: PASS; job `91816522849`; SDK `10.0.100`; 10 TRX files, 99 tests, 0 failures, 0 errors, 0 skipped; artifact `throneforge-test-results-windows-latest-30852764951`.
  - `ubuntu-latest`: PASS; job `91816522702`; SDK `10.0.100`; 10 TRX files, 99 tests, 0 failures, 0 errors, 0 skipped; artifact `throneforge-test-results-ubuntu-latest-30852764951`.
  - Both artifacts were downloaded and independently parsed. No `Overwriting results file` warning appeared in either hosted log.
- Task 3 private result: the exact official BepInEx `5.4.23.5` Windows x64 asset was verified from GitHub with asset ID `352395699`, size `639118`, and observed SHA-256 `82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4`. The disposable copy launched before and after installation; preloader and chainloader initialized, 0 custom plugins were loaded, and no warnings/errors were observed.
- The loader transaction was rolled back in the disposable copy. The copied fingerprint after rollback and the original fingerprint after the experiment matched `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`; original readiness returned to `ReadyForReversibleTest` and original loader indicators remained absent.
- No BepInEx, game binary, raw log, archive, or experiment manifest was committed. The sanitized report is `docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-loader-smoke-test.md`.
- The fingerprint-specific report now records `netstandard2.1 candidate`, confidence `Medium`, basis `netstandard compatibility surface plus Unity 2022.3 evidence`, `ReadyForReversibleTest`, and the bounded `globalgamemanagers` note under `Inspection limitations`.

## M1 task 3 validation status

- PR #2 is merged into protected `main` at `d3f1bb4fde9f77efbb84349f440385cc89002c86`.
- Main validation run `30853440786` passed on Windows and Ubuntu with SDK `10.0.100`; each runner uploaded 10 TRX files representing 99 tests, with 0 failures and 0 skips.
- The active branch is `agent/m1-loader-smoke-test`, created from that merged baseline.
- The provisional candidate was official BepInEx 5 Unity Mono x64 `5.4.23.5` for the documented fingerprint only. The experiment used an external disposable copy and left the original installation untouched.
- Private outcome is `Passed`; the original installation fingerprint was unchanged before and after the experiment, and the disposable copy fingerprint was restored after rollback.
- Final hosted validation passed in run [30857539959](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30857539959) for commit `634d3e97552d4f9ea619a2b7d9359ffc8bb1cb68`:
  - `windows-latest`: PASS; SDK `10.0.100`; 11 TRX files, 130 tests, 0 failures, 0 errors, 0 skipped.
  - `ubuntu-latest`: PASS; SDK `10.0.100`; 11 TRX files, 130 tests, 0 failures, 0 errors, 0 skipped.
  - Both artifacts were downloaded and independently parsed. No `Overwriting results file` warning appeared in either hosted log.
- The focused Loader-Smoke-Test suite represented 31 tests in the final hosted total. No hosted job ran the real game or loader experiment.

### M1 task 3 review follow-up status

- Original-installation report claims are derived from structured post-check state: pending after preflight, passed only after a complete successful post-check, failed with sanitized categories, or deferred during manual closure.
- `Baseline`, `Install`, `Launch`, `Verify`, and `Rollback` require an existing schema/task-versioned baseline; only `Prepare` and a fresh `Full` run may create one. Post-install modes additionally require an existing transaction plan.
- Recovery-marker persistence is explicit. Active profiles are never modified when marker writing fails; the result, report, and CLI expose `marker unavailable` and the redacted manual rollback instruction.
- Local Task 3 hardening validation with the installed parent-directory SDK `10.0.110`: locked restore PASS, Release build PASS with 0 warnings/errors, full solution tests PASS with 154 tests (55 LoaderSmokeTest, 80 Discovery, and 11 Architecture tests), and Contracts tests PASS with 1 test. Focused review-follow-up tests pass 24/24. `dotnet format --verify-no-changes --no-restore` remains blocked locally because SDK `10.0.100` is not installed; hosted exact-SDK validation is recorded below.
- Final hosted review-follow-up validation passed in run [30864308531](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30864308531) for implementation commit `de3159e85c177526c2dafc6b5a60fa80e38c0bc9`: Windows and Ubuntu each used SDK `10.0.100`, uploaded 11 TRX files representing 154 tests, and reported 0 failures, 0 errors, and 0 not-executed tests. Both artifacts were downloaded and parsed independently; no `Overwriting results file` warning occurred.

## M1 Task 4 completed evidence

- Historical branch: `agent/m1-plugin-runtime-boundary-hardening`.
- Baseline: reviewed Task 4 head `18f7b1135b6aaba04290198f355e6e9ac6a97b5d`, based on merged `main@06554d845a9fe46132c1a19ec0c2f18b8722acf2`.
- Scope: bind portable full-trust code-mod integrity, approval, compatibility, request, and admission-decision evidence to one exact artifact and game build before any future loader activation.
- Explicitly out of scope: assembly loading, BepInEx/Harmony/Unity references, plugin TFM selection, lifecycle bindings, game API inspection, catalog export, and custom waves.
- Implemented in this branch: canonical immutable mod identity/version and SHA-256 rules, structured artifact/game-bound integrity and approval evidence, fingerprint-bound adapter evidence, a redesigned activation request, and a deterministic admission-binding digest. The public API remains unchanged in scope and portable in dependency/signature shape; binary target-framework compatibility is not proven.
- Local focused validation with SDK `10.0.110`: Contracts tests PASS with 27 tests; Runtime tests PASS with 21 tests. Full solution restore/build/test PASS with 212 tests (12 Architecture, 27 Contracts, 21 Runtime, 80 Discovery, 66 LoaderSmokeTest, and the remaining skeleton suites). Exact-SDK formatting remains pending because SDK `10.0.100` is not installed locally.
- Hosted validation for implementation commit `382fe877f5685bb71d77ee6b5cf04afc8a57a7bf`: run `30878049044` passed on Windows and Ubuntu with SDK `10.0.100`; each runner uploaded 11 TRX files representing 212 tests, with 0 failures, errors, or skips. Both artifacts were independently parsed and no `Overwriting results file` warning appeared.
- No plugin is loaded and no game-facing compatibility conclusion has been claimed. Plugin TFM, Harmony compatibility, lifecycle bindings, game APIs, catalog extraction, and custom waves remain unverified. Evidence records are trusted runtime inputs, not cryptographic signatures or an OS sandbox.

## M1 Task 5 baseline evidence

- Branch: `agent/m1-plugin-load-smoke-test`, based on merged `main@5f4b4dd0714d0cffaf9f3267b6f0651ecf6e043e`.
- Scope: a synthetic, repository-only assembly-load probe that re-runs the existing admission gate immediately before loading one test fixture into a collectible context. The baseline did not yet bind arbitrary sidecars or prove unload observation.
- Explicitly out of scope: BepInEx installation, Thronefall launch, private game inspection, plugin invocation, lifecycle binding, Unity/Harmony/game references, game API inspection, catalog extraction, and custom waves.
- The exact SDK `10.0.100` is not installed on this workstation; local validation distinguishes SDK `10.0.110` compile/test feedback from hosted exact-SDK evidence.
- Local Task-5 validation with SDK `10.0.110` from the parent directory: restore PASS, Release build PASS with 0 warnings/errors, full solution tests PASS with 226 tests (14 PluginLoadTest, 12 Architecture, 27 Contracts, 21 Runtime, 80 Discovery, 66 LoaderSmokeTest, and remaining skeleton tests), focused PluginLoadTest tests PASS with 14, and architecture tests PASS with 12. `dotnet format --verify-no-changes --no-restore` was attempted but is blocked because the repository-pinned SDK `10.0.100` is not installed locally.
- Final pre-hardening hosted Task-5 validation: run [30880625516](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30880625516), head `10897f7b03d45f1e470b5930a9dc1341939cde6f`. Windows and Ubuntu used SDK `10.0.100`; each uploaded 12 TRX files representing 226 tests with 0 failures, 0 errors, and 0 skips. Both artifacts were independently downloaded and parsed; no `Overwriting results file` warning occurred. The first run `30880510322` failed only because fixture projects were included in the expected-project count; the workflow now filters for `IsTestProject=true`.

## M1 Task 5 hardening status

- Branch: `agent/m1-plugin-load-smoke-test-hardening`, created from reviewed head `10897f7b03d45f1e470b5930a9dc1341939cde6`.
- The probe now performs metadata-only PE/metadata preflight. It records the primary SHA-256, exact shared API/Contracts identities, trusted platform references, and non-platform references; arbitrary sidecars, duplicate API/Contracts identities, detectable native imports, and module initializers are rejected before `LoadFromStream`.
- The collectible context no longer uses `AssemblyDependencyResolver`. It shares only the exact default-context API/Contracts identities, defers trusted platform references, rejects other managed references, and rejects unmanaged resolution.
- Contract inspection requires exactly one public, top-level, non-abstract, closed class implementing the shared `IThroneForgeMod`. Internal, nested, abstract, open-generic, duplicate, and private-duplicate-contract shapes are covered by source-only fixtures.
- The primary file is captured once; the same captured bytes are hashed, admitted, and passed to `LoadFromStream`. The result distinguishes unload requested, unload observed, and bounded unload-not-observed states. Assembly loading remains full-trust; only explicit constructor/lifecycle invocation is absent, and module initializers are rejected for this synthetic slice.
- Local focused validation with SDK `10.0.110`: build PASS with 0 warnings/errors; PluginLoadTest tests PASS with 27 tests. Exact SDK `10.0.100` is not installed locally, so exact local `dotnet format` remains unavailable.
- Final hosted validation passed in GitHub Actions run [30895225156](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30895225156) for head `bb32de1dd211ddd5174f8d8f6e6490164da970db`. Windows and Ubuntu each used SDK `10.0.100`, uploaded 12 TRX files representing 239 tests, and reported 0 failures, 0 errors, and 0 skipped tests. Both artifacts were downloaded and independently parsed; no `Overwriting results file` warning occurred. The first attempt `30895090026` failed only on the formatter's import-order check for `PluginLoadProbeTests.cs`; the one-line correction was pushed as `bb32de1` and the complete follow-up run passed.

## M1 Task 5 final native-image and load-context hardening

- The pre-fix branch-head evidence was run [30895740658](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30895740658) for documentation head `1649336681fd76cb1f66623d151179015c812fcb`: Windows and Ubuntu each used SDK `10.0.100`, uploaded 12 TRX files representing 239 tests, and reported 0 failures, errors, or skips.
- The correction requires a CLR header, `CorFlags.ILOnly`, no `CorFlags.NativeEntryPoint`, zero `ImplMap`/P/Invoke entries, and verification that the loaded assembly belongs to the expected collectible context.
- New tests use only temporary PE mutations and source-level assertions; no compiled mixed-mode or C++/CLI fixture is committed.
- Local SDK `10.0.110` validation passes restore, format, Release build, the full 244-test suite, 32 focused PluginLoad tests, 12 Architecture tests, and 27 Contracts tests. Exact pinned SDK validation passed in hosted CI below; SDK `10.0.100` is not installed locally.
- Final hosted validation passed in run [30978672030](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30978672030) for head `a8595e7b56b80ab338e5d148c1ba1f13455598d4`. Windows and Ubuntu each used SDK `10.0.100`, uploaded 12 TRX files representing 244 tests, and reported 0 failures, 0 errors, and 0 skipped tests. Both artifacts were downloaded and independently parsed; no `Overwriting results file` warning occurred.

## M1 Task 6 completed evidence

- Private command: the local-only PowerShell `Full` harness was run with explicit original game path, external experiment root, official BepInEx archive, fixed game fingerprint, and fixed archive digest; absolute paths are intentionally omitted here.
- Private result: `Passed` for fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`, backend Mono, x64 executable, Unity `2022.3.62f2`, BepInEx `5.4.23.5`, and evidence-selected `netstandard2.1`.
- Package: exactly three files (synthetic plugin, `ThroneForge.API`, `ThroneForge.Contracts`); package SHA-256 `54ced96a142040cc96374bbb86d28f5a0e2735271102aff2cf4486a40d549020`; archive digest matched the fixed official SHA-256.
- Bootstrap evidence: preloader and chainloader initialized, exactly one synthetic plugin was observed, the nonce-bound readiness marker matched, API/Contracts identities matched, and no lifecycle marker, fatal error, or error evidence was present.
- Safety evidence: original complete manifest unchanged; original runtime/readiness post-verification passed; disposable rollback was verified; no private process remained active; no loader/plugin artifact was committed.
- The sanitized report is `docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-synthetic-plugin-smoke-test.md`.
- Hosted validation for documentation baseline head `dea83ef93f5b1d8d04779cf4955bc90490f9b00e` passed in run [30990848363](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30990848363). Windows and Ubuntu each used SDK `10.0.100`, uploaded 13 TRX files representing 265 tests, and reported 0 failures, 0 errors, and 0 skips. Both artifacts were independently parsed; no `Overwriting results file` warning occurred. The current follow-up commit only records this already completed CI result.

## Next task

Task 6 is complete. M1 Task 7 may be planned separately after this branch is reviewed and merged; no lifecycle or game-API compatibility is claimed.

### M1 Task 3 earlier hardening evidence

- Earlier hardening baseline: branch `agent/m1-loader-smoke-test-hardening`, based on pre-hardening head `697419a2adba75933f02b4009a16cddf3ff4b0b6`.
- Historical private bootstrap result: `Passed` for the documented fingerprint, with BepInEx `5.4.23.5` preloader/chainloader evidence and zero custom plugins. The historical run retained compatibility-fingerprint and loader-indicator post-checks, but did not retain complete original/disposable manifests; the report now says so explicitly.
- Harness hardening now requires complete original and disposable manifest equality, fresh-copy or manifest-backed resume validation, real post-runtime/readiness checks, guarded post-apply rollback, manual-closure recovery state, and a repository-derived committed report path.
- Local hardening validation with the installed parent-directory SDK `10.0.110`: locked restore PASS, Release build PASS with 0 warnings/errors, full solution tests PASS with 146 tests (including 47 LoaderSmokeTest, 80 Discovery, and 11 Architecture tests), and Contracts tests PASS with 1 test. The repository-pinned `dotnet format` command remains locally blocked because SDK `10.0.100` is not installed; hosted CI is required for exact-toolchain format verification.
- The private experiment was not rerun. No loader, game binary, archive, raw log, recovery marker, or private path was committed.
- Hosted hardening validation passed in run [30860012681](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30860012681) for implementation commit `1643cdb4e26f3e5d0890b7b203df8904ec77795c`: Windows PASS with SDK `10.0.100`, 11 TRX files, 146 tests, 0 failures/errors/skips; Ubuntu PASS with SDK `10.0.100`, 11 TRX files, 146 tests, 0 failures/errors/skips. Both artifacts were downloaded and parsed independently; no `Overwriting results file` warning occurred.
- Final documentation follow-up head `bc1f400231ab4b1b825fdc21af438520ac5a6af6` was independently validated by run [30860399073](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30860399073) with the same Windows/Linux result: 11 TRX files and 146 tests per runner, 0 failures/errors/skips, exact SDK `10.0.100`, and no overwrite warning. This follow-up changed documentation only.

### M1 Task 3 final transaction-state correction status

- The persisted loader state is now schema- and task-versioned, atomically written, and bound to the expected fingerprint, baseline manifest identity, exact archive name, and observed archive SHA-256.
- `Applied` and `LaunchObserved` are accepted only after full applied-profile verification. `FailedAndRolledBack` and empty/stale/mismatched states cannot be used by staged `Launch` or `Verify`.
- Every persisted destination and backup path is revalidated for normalized containment before staged operations and rollback. Staged `Verify` requires recorded BepInEx 5.4.23.5, preloader, chainloader, zero custom plugins, and zero fatal loader errors.
- Local synthetic validation of the correction passed with SDK `10.0.110` from the parent directory: locked restore PASS, Release build PASS with 0 warnings/errors, full-solution tests PASS with 165 tests (66 LoaderSmokeTest, 80 Discovery, and 11 Architecture tests), and focused LoaderSmokeTest tests PASS with 66 tests. Local format verification remains blocked because SDK `10.0.100` is not installed; hosted exact-SDK formatting passed.
- Final hosted validation for implementation commit `08940f274d00d6c681c08d36364deedb04474a3d` passed in run [30866207996](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30866207996): Windows and Ubuntu each used SDK `10.0.100`, uploaded 11 TRX files representing 165 tests, and reported 0 failures, 0 errors, and 0 skipped tests. Both artifacts were downloaded and independently parsed; no `Overwriting results file` warning occurred.
- The private loader experiment was not rerun. No loader/game binary, archive, raw log, transaction state, recovery marker, private path, or other prohibited artifact was committed.

## Handoff

After this branch is reviewed and merged, M1 Task 7 may be planned separately. This branch must not be extended with Harmony, game-method inspection, lifecycle bindings, or custom waves.
