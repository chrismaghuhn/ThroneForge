# ThroneForge

ThroneForge is intended to become a maintainable, Forge-like mod SDK and no-code/low-code authoring ecosystem for Thronefall. The long-term design combines a stable public API, a versioned game adapter, validated data-only packages, and desktop authoring tools.

## Current status

This repository contains the completed M0 architecture skeleton and the first M1 local-only discovery tool. It is not a functioning Thronefall mod: no loader, game-facing runtime, catalog exporter, lifecycle binding, or custom-wave implementation exists.

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

M0 architecture, hardening, and the final TRX artifact-preservation fix passed hosted Windows/Linux CI in run [30836315556](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30836315556). M1 discovery task 1 is underway on `agent/m1-discovery`; synthetic validation and one sanitized local evidence report exist, while hosted validation remains pending. `main` is now the protected default branch. ThroneForge remains an architecture skeleton, not a functioning Thronefall mod; discovery must document evidence without guessing game internals.

## License

TODO: The repository owner must select and approve a software license before accepting external contributions. No license is granted by this repository yet.
