# STATUS

## Current milestone

M1 - Thronefall discovery spike, task 1 hardening on `agent/m1-discovery-hardening`

## State

M0 is complete and the repository governance setup is complete in GitHub. M1 discovery task 1 is complete on `agent/m1-discovery`; this branch hardens its output protection, executable selection, bounded hashing, tests, and documentation before merge. M1 overall remains incomplete.

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
- Created `agent/m1-discovery-hardening` from `f87ad9bb7f483f3128cef87f53525a4cb60c48c5` to close review findings before merge; no loader or runtime integration has started.

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

The hardening implementation is complete and hosted-verified on `agent/m1-discovery-hardening`; M1 task 1 is merge-ready, while M1 task 2 has not started.

- Output roots are validated before any output directory is created: the game root, descendants, existing reparse points, and reparse-point parents are rejected. Filesystem failures are converted to sanitized `DiscoveryException` messages.
- Main-executable selection is deterministic: unique top-level `*_Data` base-name match, then installation-directory-name match, then exactly one valid top-level PE executable; multiple candidates remain ambiguous and produce architecture `Unknown`.
- Selected compatibility files and Unity-version evidence use bounded single-open reads where applicable; fingerprints remain deterministic.
- New regression coverage passes locally: output protection, sibling-path handling, reparse points, renamed installations, crash-handler ordering, ambiguous/unique executables, CLI redaction, deterministic fingerprints, and report non-creation.
- Local full solution validation with installed SDK `10.0.110` from the parent directory: restore PASS, Release build PASS (0 warnings/0 errors), full tests PASS (45 passed, 0 failed, 0 skipped), architecture tests PASS (11), and Contracts tests PASS (1).
- Local `dotnet format --verify-no-changes --no-restore` could not run because the formatter's build host resolves the repository's exact SDK requirement `10.0.100`, which is not installed on this machine. The exact pinned format/build/test path remains subject to hosted CI.
- Private report re-run passed with the sanitized evidence unchanged: `Mono`, `X64`, Unity `Unknown`, fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`, and no absolute path, username, machine name, or temporary file.

Hosted verification passed in GitHub Actions run [30843657857](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30843657857) for hardening commit `7d206ac19ebb26147b5c96833e4029e02cdc0d64`:

- `windows-latest`: PASS; job `91786534020`; SDK `10.0.100`; 10 TRX files representing 45 tests, 0 failed, 0 errors; artifact `throneforge-test-results-windows-latest-30843657857` (artifact `8867871535`).
- `ubuntu-latest`: PASS; job `91786534027`; SDK `10.0.100`; 10 TRX files representing 45 tests, 0 failed, 0 errors; artifact `throneforge-test-results-ubuntu-latest-30843657857` (artifact `8867814159`).

Both jobs completed locked restore, exact-SDK information, format verification, Release build, full tests, TRX completeness verification, and upload. Both downloaded artifacts were independently parsed. No `Overwriting results file` warning occurred in either log, and each artifact contains one TRX per test project. No local game installation was available to CI.

## Unverified assumptions

- Backend `Mono`, executable architecture `X64`, and fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d` are verified only for the documented local installation fingerprint.
- Unity version remains `Unknown`; loader, Harmony compatibility, target framework, private members, lifecycle hooks, game APIs, and wave representation remain unknown.
- The M0 external-tool target is planned as `net10.0` because the specification names .NET 10 as the active LTS at its research checkpoint; the game-facing target remains provisional until M1 evidence.
- No game-facing behavior is implemented or claimed.

## Risks and blockers

- The host's default PATH still does not contain `dotnet`; the only directly available SDK is x86 `10.0.110`, while CI and maintainers need exact `10.0.100` for canonical validation.
- The local game directory contains proprietary files and must remain ignored and outside Git history.
- The M0 baseline is committed; future changes must continue to exclude the local game directory.
- Exact SDK `10.0.100` local formatting/validation remains unverified; hosted CI is required for that pinned-toolchain result.
- The private report records only local layout evidence: Mono indicators were found under `MonoBleedingEdge` and `thronefall_Data/Managed`, plus `Assembly-CSharp.dll`; Unity version evidence was unavailable. No loader, target framework, lifecycle hook, catalog source, or runtime compatibility conclusion is claimed.
- The hardening branch is merge-ready; merge into protected `main` remains a repository-owner action. The next M1 task must wait until that merge and must remain limited to loader/managed-runtime compatibility discovery.

## Next task

Merge and protect the reviewed `agent/m1-discovery-hardening` result, then limit the next M1 investigation to loader and managed-runtime compatibility discovery. Do not install or execute a loader, inspect game methods, or implement lifecycle/custom-wave behavior.
