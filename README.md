# ThroneForge

ThroneForge is intended to become a maintainable, Forge-like mod SDK and no-code/low-code authoring ecosystem for Thronefall. The long-term design combines a stable public API, a versioned game adapter, validated data-only packages, and desktop authoring tools.

## Current status

Task 6 is complete and merged. M1 Task 7 remains incomplete on `agent/m1-lifecycle-binding-smoke-test` and is limited to the public Unity `Application.quitting` lifecycle binding. The two earlier private results remain historical `Failed`, and the one authorized final private run failed at `LoaderLaunch` with `loader-launch-failed`; no further lifecycle run is authorized. Its one permitted recovery-only rollback returned `recovery-runtime-drift` before loader mutation. The current correction makes the C# `LifecycleExperimentOrchestrator` the single owner of typed stage evidence, primary failure persistence, cleanup/postchecks, disposable runtime verification, and sanitized report generation. PowerShell is only an explicit-input wrapper for `run-lifecycle-experiment`. The task does not inspect Thronefall internals, use Harmony, access gameplay state, or implement catalog/custom-wave behavior.
The production-path correction passed hosted run `31022292522` on Windows and Ubuntu with SDK `10.0.100`, 13 TRX files and 339 tests per runner; no private run was performed for that correction. The current repository-only correction shares the loader-only comparison for normal and recovery cleanup, binds discovered executable evidence to the supplied relative path, derives the repository HEAD commit, and exposes a supported C# rollback wrapper mode. Implementation head `2139d41812b5da09c620a1685a217de1caa3510e` passed hosted run `31025440280`; final sanitized report head `e0bfd3958d4f2add190877b436737f7a0946b6e9` passed final hosted run `31028231111` with SDK `10.0.100`, 13 TRX files and 350 tests per runner. Recovery hardening head `1549552810e826afc415355f87742faaecafc354` passed hosted run `31032572956` with 355 tests per runner; all counters were clean and no TRX overwrite warning occurred. The one authorized final private run failed at `LoaderLaunch` with `loader-launch-failed` before package admission or lifecycle evidence; its recovery-only rollback returned `recovery-runtime-drift`, and no retry is authorized.

The current PR #7 review correction is repository-only. Plugin-deployed recovery removes the exact ownership-bound plugin before checking rollback drift and preserves the removal result. BaselineLaunch and LoaderLaunch expose typed manual-closure/process-active evidence; active profiles remain untouched and produce recovery-required results. No private run was performed for this correction.

Correction head `1cd903c0aa1942d793409893d458939660e40c9d` passed hosted run `31039015040` on Windows and Ubuntu with SDK `10.0.100`: 13 TRX files and 360/360 tests passed per runner, with no failure, error, timeout, abort, inconclusive, not-executed or skipped result and no TRX overwrite warning.

The follow-up review correction preserves `PluginRemovalStatus=NotRequired` through later recovery failures when no plugin was deployed. BaselineLaunch manual closure now records `NotApplied` and `no-loader-cleanup-required` without emitting a loader-rollback command. No private execution was performed.

Correction head `bb960efd9e0715639807469a109b3ce28f700c72` passed PR run `31041425981` and push run `31041429428` on Windows and Ubuntu with SDK `10.0.100`; each runner produced 13 TRX files and 362/362 passing tests with no failure, error, timeout, abort, inconclusive, not-executed or skipped result.

This repository contains the completed M0 architecture skeleton, M1 discovery tasks 1 and 2, the merged Task 3 loader-bootstrap smoke test and hardening, the merged Task 4 portable plugin/runtime admission boundary, the merged Task 5 repository-only synthetic plugin-load probe, and the Task 6 disposable synthetic-plugin smoke-test harness under hardening. The repository remains a game-free architecture and test surface; it is not a functioning Thronefall mod and has no game-facing runtime, catalog exporter, lifecycle binding, or custom-wave implementation.

Task 6 keeps private experiments separate from hosted CI. The harness requires a fingerprint-bound ownership record, derives loader/profile readiness, recaptures and admits the exact three package files immediately before transactional deployment, validates package metadata, and records actual runtime API/Contracts identities. The earlier private attempt remains recorded as `Failed` because a Task-3/Task-6 baseline-state filename mismatch stopped deployment before plugin files were written; rollback and original post-checks passed. After the canonical state-path correction, pre-private run `30998768487` and final run `30999995802` passed with 13 TRX files and 279 tests per runner using SDK `10.0.100`, and one fresh private rerun passed with exact package recapture, one plugin/nonce marker, actual API/Contracts identity evidence, plugin removal, loader rollback, complete disposable restoration, and unchanged original verification. Task 7's hosted synthetic run `31004703784` passed with 13 TRX files and 304 tests per runner, but its final authorized private run failed at `LoaderLaunch`; no lifecycle markers or binding conclusion are claimed. The one recovery-only rollback then failed with `recovery-runtime-drift` before loader mutation. No plugin, loader, game binary, raw log, archive, nonce, or private path is committed. See the [Task-6 report](docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-synthetic-plugin-smoke-test.md), the [Task-7 report](docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-lifecycle-binding.md), [Task-6 design](docs/superpowers/specs/2026-08-05-m1-disposable-bepinex-plugin-smoke-test-design.md), and [discovery safety guide](docs/discovery/README.md).

ThroneForge does not distribute Thronefall binaries, copied game assets, decompiled source, or modified game executables.

## Prerequisites

- .NET SDK `10.0.100`, selected by [`global.json`](global.json).
- A clean checkout; a Thronefall installation is not required for synthetic builds and tests. Private discovery requires an explicit path to a legally obtained local installation.

## Local validation

```bash
dotnet --info
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

The local game directory, build output, logs, and private game reference paths are ignored by Git. Do not stage them. To run the local-only discovery tool, see [`docs/discovery/README.md`](docs/discovery/README.md).

## Roadmap and specification

- [Implementation roadmap](PLAN.md)
- [Master specification](docs/THRONEFORGE_SPEC.md)
- [Current project status](STATUS.md)

## Development status disclaimer

M0 architecture, hardening, and the final TRX artifact-preservation fix passed hosted Windows/Linux CI. M1 discovery tasks 1 and 2 are complete on protected `main`; PR #2 merged at `d3f1bb4fde9f77efbb84349f440385cc89002c86`. M1 Task 3 and its hardening merged by PR #3 at `06554d8`; the historical private bootstrap result retains its complete-manifest limitation. M1 Task 4 and its trust-evidence hardening merged by PR #4 at `5f4b4dd0714d0cffaf9f3267b6f0651ecf6e043e`. M1 Task 5 is merged and hosted-verified. M1 Task 6 is merged and hosted-verified with one corrected private synthetic-plugin pass. M1 Task 7 remains repository-only in this correction: the real CLI-to-C# orchestrator path is implemented, but no private lifecycle run is authorized and the public Unity binding remains unverified. No game API, Harmony, catalog exporter, or custom-wave functionality is claimed.

## License

TODO: The repository owner must select and approve a software license before accepting external contributions. No license is granted by this repository yet.

## Current Task-7 production-path correction

The current Task-7 correction keeps the private lifecycle experiment disabled. The production C# orchestrator now owns Task-6 ownership, typed stage evidence, side-effect-preserving cleanup, manual-closure recovery, package/path binding, and independent disposable/original postchecks; the PowerShell entry point is only an argument wrapper. Repository-only validation is required before a separately authorized private run. No new private run is claimed here.
