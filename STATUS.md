# STATUS

## Current milestone

M1 - Thronefall discovery spike, task 1: local-only installation inspection

## State

M0 is complete and the repository governance setup is now complete in GitHub. M1 has started on `agent/m1-discovery`; no Thronefall discovery conclusion has been made yet.

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
- Hosted CI passed in GitHub Actions run [30839919115](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30839919115) for commit `912be5c7274014ecde0c3fa1deac2ae68e48a1a4`:
  - `ubuntu-latest`: PASS; job `91774176463`; SDK `10.0.100`; 10 TRX files representing 35 tests and 0 failed/errors; artifact `throneforge-test-results-ubuntu-latest-30839919115` (artifact `8866359385`).
  - `windows-latest`: PASS; job `91774176512`; SDK `10.0.100`; 10 TRX files representing 35 tests and 0 failed/errors; artifact `throneforge-test-results-windows-latest-30839919115` (artifact `8866383818`).
- Hosted logs and both downloaded artifact directories were independently checked; no `Overwriting results file` warning occurred.

Final hosted verification passed in GitHub Actions run [30836315556](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30836315556) for commit `0eb597304a1f188c428585e05e014517532fd11c`:

- `windows-latest`: PASS; job `91762246279`; SDK `10.0.100`; 9 TRX files representing 19 tests and 0 failed/errors; artifact `throneforge-test-results-windows-latest-30836315556` (artifact `8865001699`).
- `ubuntu-latest`: PASS; job `91762246181`; SDK `10.0.100`; 9 TRX files representing 19 tests and 0 failed/errors; artifact `throneforge-test-results-ubuntu-latest-30836315556` (artifact `8864985942`).

Both jobs completed locked restore, format verification, Release build, full tests, the completeness check, and test-result upload. The two artifacts were downloaded and independently parsed. No `Overwriting results file` warning occurred in either test log. The run used no local game installation.

## Unverified assumptions

- Thronefall executable architecture, Unity version, scripting backend, loader, Harmony compatibility, target framework, build fingerprint, private members, lifecycle hooks, and wave representation have not been discovered.
- The M0 external-tool target is planned as `net10.0` because the specification names .NET 10 as the active LTS at its research checkpoint; the game-facing target remains provisional until M1 evidence.
- No game-facing behavior is implemented or claimed.

## Risks and blockers

- The host's default PATH still does not contain `dotnet`; the only directly available SDK is x86 `10.0.110`, while CI and maintainers need exact `10.0.100` for canonical validation.
- The local game directory contains proprietary files and must remain ignored and outside Git history.
- The M0 baseline is committed; future changes must continue to exclude the local game directory.
- The local Git remote's symbolic `origin/HEAD` still points to `agent/m0-bootstrap`; GitHub's web settings independently show `main` as the default branch. This local symbolic ref can be refreshed with `git remote set-head origin -a` when network metadata is refreshed.
- Exact SDK `10.0.100` local formatting/validation remains unverified; hosted CI is required for that pinned-toolchain result.
- The private report records only local layout evidence: Mono indicators were found under `MonoBleedingEdge` and `thronefall_Data/Managed`, plus `Assembly-CSharp.dll`; Unity version evidence was unavailable. No loader, target framework, lifecycle hook, catalog source, or runtime compatibility conclusion is claimed.

## Next task

Run exact pinned-toolchain CI for `agent/m1-discovery` and record both runner results. Then limit the next M1 investigation to loader/runtime compatibility discovery.
