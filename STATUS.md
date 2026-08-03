# STATUS

## Current milestone

M0 - Hardening and hosted-CI verification

## State

M0 implementation complete locally. Hosted Windows/Linux CI verification pending.

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
- Added repository-wide dependency declaration scanning for central props, source projects, and source lock files, with synthetic forbidden/allowed/harmless-input tests.
- Added `README.md`, `SECURITY.md`, and `CONTRIBUTING.md` without selecting a license.

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

Hosted Windows/Linux CI verification is pending for the hardening branch and must not be inferred from the local results above. The workflow is configured but has not yet produced a run identifier or hosted SDK output.

## Unverified assumptions

- Thronefall executable architecture, Unity version, scripting backend, loader, Harmony compatibility, target framework, build fingerprint, private members, lifecycle hooks, and wave representation have not been discovered.
- The M0 external-tool target is planned as `net10.0` because the specification names .NET 10 as the active LTS at its research checkpoint; the game-facing target remains provisional until M1 evidence.
- No game-facing behavior is implemented or claimed.

## Risks and blockers

- The host's default PATH still does not contain `dotnet`; CI and maintainers need a .NET 10 SDK matching `global.json`.
- The local game directory contains proprietary files and must remain ignored and outside Git history.
- The M0 baseline is committed; future changes must continue to exclude the local game directory.
- The hosted Windows/Linux CI workflow has not yet completed; its result, run identifier, tested SHA, and per-runner SDK output must be recorded after GitHub executes it.
- The remote repository currently has no `main` branch; the repository owner must create or promote `main`, set it as default, and protect it before a normal M0 hardening PR can target it.

## Next task

Finish M0 hardening by pushing this branch, wait for a real hosted Windows/Linux CI run, and establish protected `main`. Only then begin M1, task 1: create a local-only discovery tool that accepts an explicit Thronefall installation path, detects the managed Mono versus IL2CPP layout and executable architecture, computes a sanitized fingerprint from local metadata, and writes `docs/discovery/<fingerprint>.md` without copying or committing game binaries or assets.
