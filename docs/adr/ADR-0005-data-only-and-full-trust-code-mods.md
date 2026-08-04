# ADR-0005: Data-only content versus full-trust code mods

- Status: Accepted for M0
- Date: 2026-08-03

## Context

Declarative content can be bounded and validated, but arbitrary .NET assemblies execute inside the game process and cannot be securely sandboxed by this SDK. Users need an accurate trust distinction.

## Decision

Content and logic packages are data-only by default. They may contain validated manifests, portable content, approved small assets, localization, configuration, and built-in graph data; they must not contain assemblies, scripts, native libraries, executables, arbitrary expressions, or automatically invoked network endpoints. Code mods are a separate, explicitly labeled full-trust category. The manager must require explicit user approval before enabling a new code mod, bind the approval to the exact canonical mod identity, package SHA-256, and game fingerprint, and load it only after independently produced integrity and compatibility evidence. The M1 Task 4 admission boundary represents these prerequisites as portable records and returns a decision before any future loader is allowed to load or invoke code. Safe mode can disable third-party code mods.

## Consequences

- Package validation can offer meaningful constraints for data-only mods without claiming to sandbox code.
- Code-mod capabilities remain an opt-in escape hatch and require clearer diagnostics, restart behavior, and recovery handling.
- M1 Task 4 contains no code-mod loader and no executable content. Its records are trusted runtime inputs, not cryptographic signatures or an OS sandbox. Admission approval is not a sandbox, plugin-load proof, binary target-framework decision, or game-API compatibility claim.
