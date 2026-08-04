# ADR-0006: Full-trust code-mod admission boundary

- Status: Accepted for M1 Task 4 boundary design; loader implementation remains unstarted
- Date: 2026-08-04

## Context

ThroneForge distinguishes data-only content from executable code mods. A code mod will eventually run in the game process and cannot be securely sandboxed by this SDK. The loader smoke test proved only that the selected BepInEx bootstrap initialized for one fingerprint; it did not prove that a ThroneForge plugin can load, that its target framework is compatible, or that any game-facing API works.

The runtime therefore needs a small, portable decision boundary before a future loader is allowed to activate code. The boundary must be usable by tests and future runtime code without importing BepInEx, Unity, Harmony, private game assemblies, or reflection names into stable projects.

## Decision

Task 4 defines three separate concerns:

1. `ThroneForge.Contracts` carries immutable code-mod identity, package-integrity, and activation-request data. These records contain normalized public identifiers and hashes only; they never contain installation paths or executable objects.
2. `ThroneForge.API` exposes the portable `IThroneForgeMod` lifecycle contract and a minimal capability context. It exposes no game objects and does not define unverified lifecycle events.
3. `ThroneForge.Runtime` evaluates a `CodeModActivationRequest` through a deterministic admission gate. The gate requires verified package integrity, supported adapter compatibility, and explicit user approval before returning `Approved`. It returns structured rejection or approval data and never loads, reflects over, invokes, or unloads an assembly.

`Approved` means only that a later loader may continue to its own loading and compatibility checks. It is not a claim that a plugin can load in Thronefall, that a target framework has been selected, that Harmony/HarmonyX is compatible, or that lifecycle/game APIs are available.

## Consequences

- The trust decision is explicit and testable before any future code-mod activation.
- The stable API and runtime remain free of game-specific dependencies and can be tested without a game installation.
- Package integrity and user approval are prerequisites, but they are not an OS-level security sandbox.
- A future loader must preserve this boundary, record the package hash and approval decision, and add separate evidence for assembly loading and game compatibility.
- Plugin target framework, assembly loading, Harmony compatibility, lifecycle bindings, and game APIs remain unverified until a later bounded task and clean-profile evidence exist.
