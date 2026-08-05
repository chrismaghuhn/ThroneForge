# ADR-0002: Target-framework split pending M1 discovery

- Status: Bootstrap smoke-test verified for the documented fingerprint; plugin/runtime integration remains provisional
- Date: 2026-08-03

## Context

External tooling should use the pinned current .NET LTS, while the injected game runtime must match the detected Thronefall scripting backend and selected loader. The fingerprint-specific local evidence now supports a Mono runtime, x64 executable, `netstandard.dll`, `mscorlib` 4.0.0.0, and Unity 2022.3.62 evidence. These findings apply only to the documented installation fingerprint.

## Decision

Task 7 uses the already documented `netstandard2.1` candidate only for the synthetic lifecycle package. This does not establish general plugin TFM or game compatibility. The lifecycle binding remains limited to public `UnityEngine.Application.quitting` evidence for the documented fingerprint. Its correction records sanitized stage/category evidence, uses the same tested host in repository tests and the generated plugin, requires strict BepInEx marker order, and independently verifies cleanup/rollback. The single corrective private run remains failed at `OriginalPreflight` with `original-preflight-failed` because the harness did not parse the discovery CLI's selected-executable output; no lifecycle evidence was produced.

M0 pins the external-tool SDK to .NET 10 (`global.json`) and uses `net10.0` as the target for external tooling. The Task 2 compatibility report provisionally recommends `netstandard2.1` for a future plugin target because the local evidence includes `netstandard.dll` and Unity 2022.3.62. It provisionally recommends the official BepInEx 5.4.23.5 Unity Mono x64 distribution for a later smoke test because the local backend and executable architecture match and the official release is the stable LTS line.

These are discovery recommendations only. The target-framework assessment records evidence-specific confidence; the current installation's `netstandard2.1` recommendation is medium confidence because it combines a netstandard compatibility surface with Unity 2022.3 evidence. Candidate selection is independent from current clean-profile readiness: existing loader/bootstrap indicators block readiness even when BepInEx 5 remains the leading candidate. A reversible clean-profile experiment for this exact fingerprint verified BepInEx 5.4.23.5 bootstrap initialization with preloader and chainloader evidence and zero custom plugins. Plugin target-framework compatibility, Harmony selection, lifecycle bindings, runtime APIs, and game-facing behavior remain unverified.

No BepInEx, Harmony/HarmonyX, Unity, or proprietary game package is selected or referenced in M0.

## Consequences

- A clean clone can build the platform-neutral skeleton without the game installed once the pinned SDK is present.
- The game-facing target may diverge from shared/external targets after the clean-profile smoke test.
- The report claims only BepInEx bootstrap compatibility for the documented fingerprint; it does not claim a loadable ThroneForge plugin, plugin TFM compatibility, Harmony compatibility, lifecycle binding, or game API compatibility.
