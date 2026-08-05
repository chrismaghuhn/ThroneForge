# ThroneForge

ThroneForge is intended to become a maintainable, Forge-like mod SDK and no-code/low-code authoring ecosystem for Thronefall. The long-term design combines a stable public API, a versioned game adapter, validated data-only packages, and desktop authoring tools.

## Current status

This repository contains the completed M0 architecture skeleton, M1 discovery tasks 1 and 2, the merged Task 3 loader-bootstrap smoke test and hardening, the merged Task 4 portable plugin/runtime admission boundary, the merged Task 5 repository-only synthetic plugin-load probe, and the Task 6 disposable synthetic-plugin smoke-test harness under hardening. The repository remains a game-free architecture and test surface; it is not a functioning Thronefall mod and has no game-facing runtime, catalog exporter, lifecycle binding, or custom-wave implementation.

Task 6 keeps private experiments separate from hosted CI. The harness now requires a fingerprint-bound ownership record, derives loader/profile readiness, recaptures and admits the exact three package files immediately before transactional deployment, validates package metadata, and records actual runtime API/Contracts identities. Hosted synthetic CI passed in runs `30994830386` and `30995632643`; the final fresh private attempt recaptured the package but failed at disposable-profile deployment-state validation before writing plugin files, then restored the profile and original installation. No plugin pass is claimed, and M1 Task 7 has not started. No plugin, loader, game binary, raw log, archive, nonce, or private path is committed. See the [Task-6 report](docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-synthetic-plugin-smoke-test.md), [Task-6 design](docs/superpowers/specs/2026-08-05-m1-disposable-bepinex-plugin-smoke-test-design.md), and [discovery safety guide](docs/discovery/README.md).

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

M0 architecture, hardening, and the final TRX artifact-preservation fix passed hosted Windows/Linux CI. M1 discovery tasks 1 and 2 are complete on protected `main`; PR #2 merged at `d3f1bb4fde9f77efbb84349f440385cc89002c86`. M1 Task 3 and its hardening merged by PR #3 at `06554d8`; the historical private bootstrap result retains its complete-manifest limitation. M1 Task 4 and its trust-evidence hardening merged by PR #4 at `5f4b4dd0714d0cffaf9f3267b6f0651ecf6e043e`. M1 Task 5 is merged and hosted-verified. M1 Task 6 hardening is hosted-verified, but its final fresh private attempt failed before plugin deployment during disposable-profile state validation; rollback and original post-checks passed. No real Thronefall plugin was loaded, and no loader, game API, lifecycle binding, catalog exporter, or custom-wave functionality is claimed.

## License

TODO: The repository owner must select and approve a software license before accepting external contributions. No license is granted by this repository yet.
