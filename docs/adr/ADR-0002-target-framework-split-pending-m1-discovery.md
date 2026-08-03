# ADR-0002: Target-framework split pending M1 discovery

- Status: Accepted for M0; revisit in M1
- Date: 2026-08-03

## Context

External tooling should use the pinned current .NET LTS, while the injected game runtime must match the detected Thronefall scripting backend and selected loader. The game installation has not yet been inspected.

## Decision

M0 pins the external-tool SDK to .NET 10 (`global.json`) and uses `net10.0` as the provisional target for compile-only project placeholders. This provisional target is not evidence that the game runtime is compatible. M1 must inspect the local installation, determine Mono versus IL2CPP, verify the loader and Harmony setup, choose the game-facing target framework, and update the game-facing projects and this ADR with the evidence and compatibility consequences.

No BepInEx, Harmony/HarmonyX, Unity, or proprietary game package is selected or referenced in M0.

## Consequences

- A clean clone can build the platform-neutral skeleton without the game installed once the pinned SDK is present.
- The game-facing target may diverge from shared/external targets after M1.
- M0 cannot claim a loadable plugin or game compatibility.

