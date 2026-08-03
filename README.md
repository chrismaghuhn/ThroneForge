# ThroneForge

ThroneForge is intended to become a maintainable, Forge-like mod SDK and no-code/low-code authoring ecosystem for Thronefall. The long-term design combines a stable public API, a versioned game adapter, validated data-only packages, and desktop authoring tools.

## Current status

This repository currently contains the M0 architecture skeleton and its hardening work. It is not a functioning Thronefall mod, and no game-facing runtime, discovery tooling, catalog exporter, or custom-wave implementation exists yet. M1 discovery must complete before any game integration is added.

ThroneForge does not distribute Thronefall binaries, copied game assets, decompiled source, or modified game executables.

## Prerequisites

- .NET SDK `10.0.100`, selected by [`global.json`](global.json).
- A clean checkout; a Thronefall installation is not required for M0 builds and tests.

## Local validation

```bash
dotnet --info
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

The local game directory, build output, logs, and private game reference paths are ignored by Git. Do not stage them.

## Roadmap and specification

- [Implementation roadmap](PLAN.md)
- [Master specification](docs/THRONEFORGE_SPEC.md)
- [Current project status](STATUS.md)

## Development status disclaimer

M0 architecture and hardening have passed hosted Windows/Linux CI. The latest TRX artifact-preservation fix is being verified on its dedicated branch before final M0 closure. M1 has not started, and repository-owner setup of a protected `main` branch is still required. ThroneForge remains an architecture skeleton, not a functioning Thronefall mod; M1 must remain a local-only discovery phase and document evidence without guessing game internals.

## License

TODO: The repository owner must select and approve a software license before accepting external contributions. No license is granted by this repository yet.
