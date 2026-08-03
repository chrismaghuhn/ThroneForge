# ADR-0002: Target-framework split pending M1 discovery

- Status: Provisional recommendation recorded during M1 task 2; hardening hosted verification pending
- Date: 2026-08-03

## Context

External tooling should use the pinned current .NET LTS, while the injected game runtime must match the detected Thronefall scripting backend and selected loader. The fingerprint-specific local evidence now supports a Mono runtime, x64 executable, `netstandard.dll`, `mscorlib` 4.0.0.0, and Unity 2022.3.62 evidence. These findings apply only to the documented installation fingerprint.

## Decision

M0 pins the external-tool SDK to .NET 10 (`global.json`) and uses `net10.0` as the target for external tooling. The Task 2 compatibility report provisionally recommends `netstandard2.1` for a future plugin target because the local evidence includes `netstandard.dll` and Unity 2022.3.62. It provisionally recommends the official BepInEx 5.4.23.5 Unity Mono x64 distribution for a later smoke test because the local backend and executable architecture match and the official release is the stable LTS line.

These are discovery recommendations only. The target-framework assessment records evidence-specific confidence; the current installation's `netstandard2.1` recommendation is medium confidence because it combines a netstandard compatibility surface with Unity 2022.3 evidence. Candidate selection is independent from current clean-profile readiness: existing loader/bootstrap indicators block readiness even when BepInEx 5 remains the leading candidate. Production game-facing target frameworks, loader dependencies, Harmony selection, lifecycle bindings, and runtime APIs remain unselected. All recommendations are provisional until a clean-profile loader smoke test succeeds.

No BepInEx, Harmony/HarmonyX, Unity, or proprietary game package is selected or referenced in M0.

## Consequences

- A clean clone can build the platform-neutral skeleton without the game installed once the pinned SDK is present.
- The game-facing target may diverge from shared/external targets after the clean-profile smoke test.
- The report does not claim a loadable plugin, loader compatibility, Harmony compatibility, lifecycle binding, or game API compatibility.
