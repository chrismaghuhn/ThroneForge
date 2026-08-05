# ADR-0005: Data-only content versus full-trust code mods

- Status: Accepted; M1 Task 5 final native-image/load-context correction complete
- Date: 2026-08-03

## Context

Declarative content can be bounded and validated, but arbitrary .NET assemblies execute inside the game process and cannot be securely sandboxed by this SDK. Users need an accurate trust distinction.

## Decision

Content and logic packages are data-only by default. They may contain validated manifests, portable content, approved small assets, localization, configuration, and built-in graph data; they must not contain assemblies, scripts, native libraries, executables, arbitrary expressions, or automatically invoked network endpoints. Code mods are a separate, explicitly labeled full-trust category. The manager must require explicit user approval before enabling a new code mod, bind the approval to the exact canonical mod identity, package SHA-256, and game fingerprint, and load it only after independently produced integrity and compatibility evidence. The M1 Task 4 admission boundary represents these prerequisites as portable records and returns a decision before any future loader is allowed to load or invoke code. Safe mode can disable third-party code mods.

## Consequences

- Package validation can offer meaningful constraints for data-only mods without claiming to sandbox code.
- Code-mod capabilities remain an opt-in escape hatch and require clearer diagnostics, restart behavior, and recovery handling.
- M1 Task 4 contains no production code-mod loader. M1 Task 5 adds only a repository-local synthetic assembly-load probe for test evidence; it does not load or invoke a Thronefall plugin. The hardening slice permits one primary managed assembly plus exact shared API/Contracts and trusted platform references, requires a CLR header marked `ILOnly` with no native entry point, rejects arbitrary sidecars/native imports/module initializers in preflight, verifies the actual collectible load context, and requires bounded collectible unload observation. Assembly loading remains full-trust and is not an OS sandbox. The records and probe are not cryptographic signatures, a binary target-framework decision, BepInEx proof, or a game-API compatibility claim. Multi-file package integrity remains out of scope.

## Task-6 experiment boundary

The disposable synthetic-plugin smoke test is a private full-trust experiment only. It builds a source-controlled template against locally evidenced BepInEx/Unity/API/Contracts references and deploys exactly one three-file package into an external disposable copy. It does not make BepInEx, Unity, or game references part of portable contracts, schemas, packaging, or production API boundaries. No real Thronefall plugin is loaded by this task.
