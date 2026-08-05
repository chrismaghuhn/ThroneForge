# ThroneForge

ThroneForge is intended to become a maintainable, Forge-like mod SDK and no-code/low-code authoring ecosystem for Thronefall. The long-term design combines a stable public API, a versioned game adapter, validated data-only packages, and desktop authoring tools.

## Current status

Task 6 is complete and merged. M1 Task 7 remains incomplete on `agent/m1-lifecycle-binding-smoke-test` and is limited to the public Unity `Application.quitting` lifecycle binding. The two permitted private results remain historical `Failed`; no further private run is authorized in the current orchestration correction. The current correction makes the C# `LifecycleExperimentOrchestrator` the single owner of typed stage evidence, primary failure persistence, cleanup/postchecks, disposable runtime verification, and sanitized report generation. PowerShell is only an explicit-input wrapper for `run-lifecycle-experiment`. The task does not inspect Thronefall internals, use Harmony, access gameplay state, or implement catalog/custom-wave behavior.
The repository-only correction is not yet hosted-validated in this working state; no private run was performed.

This repository contains the completed M0 architecture skeleton, M1 discovery tasks 1 and 2, the merged Task 3 loader-bootstrap smoke test and hardening, the merged Task 4 portable plugin/runtime admission boundary, the merged Task 5 repository-only synthetic plugin-load probe, and the Task 6 disposable synthetic-plugin smoke-test harness under hardening. The repository remains a game-free architecture and test surface; it is not a functioning Thronefall mod and has no game-facing runtime, catalog exporter, lifecycle binding, or custom-wave implementation.

Task 6 keeps private experiments separate from hosted CI. The harness requires a fingerprint-bound ownership record, derives loader/profile readiness, recaptures and admits the exact three package files immediately before transactional deployment, validates package metadata, and records actual runtime API/Contracts identities. The earlier private attempt remains recorded as `Failed` because a Task-3/Task-6 baseline-state filename mismatch stopped deployment before plugin files were written; rollback and original post-checks passed. After the canonical state-path correction, pre-private run `30998768487` and final run `30999995802` passed with 13 TRX files and 279 tests per runner using SDK `10.0.100`, and one fresh private rerun passed with exact package recapture, one plugin/nonce marker, actual API/Contracts identity evidence, plugin removal, loader rollback, complete disposable restoration, and unchanged original verification. Task 7's hosted synthetic run `31004703784` passed with 13 TRX files and 304 tests per runner, but its one permitted fresh private run failed before a loader transaction was persisted; no lifecycle markers or binding conclusion are claimed. No plugin, loader, game binary, raw log, archive, nonce, or private path is committed. See the [Task-6 report](docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-synthetic-plugin-smoke-test.md), the [Task-7 report](docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-lifecycle-binding.md), [Task-6 design](docs/superpowers/specs/2026-08-05-m1-disposable-bepinex-plugin-smoke-test-design.md), and [discovery safety guide](docs/discovery/README.md).

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
