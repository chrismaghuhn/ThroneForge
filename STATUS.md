# STATUS

## Current milestone

M0 - Repository bootstrap and architecture skeleton

## State

Complete.

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

## Validation

All commands below were run with the pinned .NET SDK 10.0.100 provisioned outside the repository because the host did not expose `dotnet` on PATH.

- `dotnet restore --locked-mode`: PASS; all projects up to date.
- `dotnet build -c Release --no-restore`: PASS; 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: PASS; 13 tests passed, 0 failed, 0 skipped.
- `dotnet format --verify-no-changes`: PASS; no output, exit code 0.
- `dotnet test tests/ThroneForge.ArchitectureTests -c Release --no-build`: PASS; 5 tests passed, 0 failed.
- `dotnet test tests/ThroneForge.Contracts.Tests -c Release --no-build`: PASS; 1 test passed, 0 failed.

The red-green architecture-test check was also performed: the initial test run failed on the intentionally absent M0 skeleton, and the post-bootstrap Release run passed all five architecture tests.

## Unverified assumptions

- Thronefall executable architecture, Unity version, scripting backend, loader, Harmony compatibility, target framework, build fingerprint, private members, lifecycle hooks, and wave representation have not been discovered.
- The M0 external-tool target is planned as `net10.0` because the specification names .NET 10 as the active LTS at its research checkpoint; the game-facing target remains provisional until M1 evidence.
- No game-facing behavior is implemented or claimed.

## Risks and blockers

- The host's default PATH still does not contain `dotnet`; CI and maintainers need a .NET 10 SDK matching `global.json`.
- The local game directory contains proprietary files and must remain ignored and outside Git history.
- The M0 baseline is being published as the repository's first commit; future changes must continue to exclude the local game directory.
- The hosted Linux CI workflow has not been executed from this desktop session; it is configured for the same locked restore/build/test/format checks.

## Next task

M1, task 1: create a local-only discovery tool that accepts an explicit Thronefall installation path, detects the managed Mono versus IL2CPP layout and executable architecture, computes a sanitized fingerprint from local metadata, and writes `docs/discovery/<fingerprint>.md` without copying or committing game binaries or assets. Stop after documenting evidence and blockers; do not guess a lifecycle hook or implement the wave bridge.
