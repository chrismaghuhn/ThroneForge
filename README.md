# ThroneForge

ThroneForge is intended to become a maintainable, Forge-like mod SDK and no-code/low-code authoring ecosystem for Thronefall. The long-term design combines a stable public API, a versioned game adapter, validated data-only packages, and desktop authoring tools.

## Current status

This repository contains the completed M0 architecture skeleton, M1 discovery tasks 1 and 2, the merged Task 3 loader-bootstrap smoke test and hardening, the merged Task 4 portable plugin/runtime admission boundary, the merged Task 5 repository-only synthetic plugin-load probe, and the completed M1 Task 6 disposable synthetic-plugin smoke test. The repository remains a game-free architecture and test surface; it is not a functioning Thronefall mod and has no game-facing runtime, catalog exporter, lifecycle binding, or custom-wave implementation.

Task 6 kept the private experiment separate from hosted CI. The source-only plugin was built only after bounded local BepInEx/Unity/API/Contracts metadata selected `netstandard2.1`; the exact three-file package was bound to the fixed game fingerprint and approved immediately before deployment. The disposable run passed, then removed the plugin and rolled back the loader. No plugin, loader, game binary, raw log, archive, nonce, or private path is committed. See the [Task-6 report](docs/discovery/1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-synthetic-plugin-smoke-test.md), [Task-6 design](docs/superpowers/specs/2026-08-05-m1-disposable-bepinex-plugin-smoke-test-design.md), and [discovery safety guide](docs/discovery/README.md).

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

M0 architecture, hardening, and the final TRX artifact-preservation fix passed hosted Windows/Linux CI. M1 discovery tasks 1 and 2 are complete on protected `main`; PR #2 merged at `d3f1bb4fde9f77efbb84349f440385cc89002c86`. M1 Task 3 and its hardening merged by PR #3 at `06554d8`; the historical private bootstrap result retains its complete-manifest limitation. M1 Task 4 and its trust-evidence hardening merged by PR #4 at `5f4b4dd0714d0cffaf9f3267b6f0651ecf6e043e`; final head run [30878236039](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30878236039) passed with 11 TRX files and 212 tests per runner using SDK `10.0.100`. M1 Task 5 final native-image/load-context hardening passed run [30978672030](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30978672030) for head `a8595e7`, with 12 TRX files and 244 tests per runner using SDK `10.0.100`; no overwrite warnings occurred. M1 Task 6 passed one private disposable synthetic-plugin run for the fixed fingerprint; hosted validation of head `99f3d68` passed in [30990572415](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30990572415) with 13 TRX files and 265 tests per runner using SDK `10.0.100`. No real Thronefall plugin was loaded, and no loader, game API, lifecycle binding, catalog exporter, or custom-wave functionality is claimed.

## License

TODO: The repository owner must select and approve a software license before accepting external contributions. No license is granted by this repository yet.
