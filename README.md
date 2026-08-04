# ThroneForge

ThroneForge is intended to become a maintainable, Forge-like mod SDK and no-code/low-code authoring ecosystem for Thronefall. The long-term design combines a stable public API, a versioned game adapter, validated data-only packages, and desktop authoring tools.

## Current status

This repository contains the completed M0 architecture skeleton, M1 discovery tasks 1 and 2, and the merged Task 3 loader-bootstrap smoke test plus its fail-closed hardening. M1 Task 4's bounded portable plugin/runtime admission boundary is undergoing evidence-binding hardening on `agent/m1-plugin-runtime-boundary-hardening`; it does not load a plugin. `ThroneForge.API` currently demonstrates a portable dependency and signature shape, while binary target-framework compatibility remains unverified. The historical private bootstrap result is retained with an explicit limitation: complete pre/post manifests were not retained. It is not a functioning Thronefall mod: no plugin is loaded, and no game-facing runtime, catalog exporter, lifecycle binding, or custom-wave implementation exists.

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

M0 architecture, hardening, and the final TRX artifact-preservation fix passed hosted Windows/Linux CI. M1 discovery task 1 and task 2 are complete on protected `main`; PR #2 merged at `d3f1bb4fde9f77efbb84349f440385cc89002c86`. Main run [30853440786](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30853440786) passed with 10 TRX files and 99 tests per runner using SDK `10.0.100`. M1 task 3's historical private bootstrap result passed with the explicit limitation that complete historical manifests were not retained; pre-hardening run [30857962381](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30857962381) represented 130 tests per runner. The Task 3 hardening and transaction-state runs remain recorded in the prior history. PR #3 merged Task 3 into `main` at `06554d8`; `main` remains the protected default branch. The prior Task 4 bounded-slice head `5d7c69a` passed hosted run [30868670093](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30868670093) with 11 TRX files and 175 tests per runner using SDK `10.0.100`. Task 4 hardening implementation head `382fe87` passed hosted run [30878049044](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30878049044) with 11 TRX files and 212 tests per runner using SDK `10.0.100`; no overwrite warnings occurred. M1 remains incomplete; no loader or plugin is claimed. ThroneForge remains an architecture skeleton, not a functioning Thronefall mod; discovery must document evidence without guessing game internals.

## License

TODO: The repository owner must select and approve a software license before accepting external contributions. No license is granted by this repository yet.
