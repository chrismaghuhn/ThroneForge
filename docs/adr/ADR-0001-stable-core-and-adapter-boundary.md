# ADR-0001: Stable core and adapter boundary

- Status: Accepted for M0
- Date: 2026-08-03

## Context

Thronefall private types and lifecycle details can change independently of the SDK. The specification requires portable contracts and services to remain useful to Studio, CLI, tests, and runtime even when the game adapter changes.

## Decision

All game-specific integration is confined to `ThroneForge.GameAdapter.*` and `ThroneForge.Bootstrap.*`. Stable projects communicate through portable contracts, schemas, packaging, diagnostics, content, logic, and explicit adapter abstractions. Adapter-facing data crosses the boundary as fingerprints, capabilities, handles, IDs, and immutable snapshots; private game objects never appear in public APIs or shared domain models. M0 contains no game assembly references, Unity references, BepInEx references, Harmony references, reflection names, or guessed game behavior.

Architecture tests inspect both project references and compiled assembly references so a forbidden dependency fails in CI.

## Consequences

- Adapter changes can be isolated from shared contracts and authoring tools.
- Game-facing projects may start as compile-only placeholders until M1 discovery supplies verified dependencies.
- Features requiring game behavior must be implemented only after local evidence is recorded under `docs/discovery/`.
- Some adapter translation code is intentionally duplicated from neither Studio nor CLI; both consume shared portable services.

