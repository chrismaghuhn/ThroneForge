# ADR-0004: JSON Schema 2020-12 and shared validation

- Status: Accepted for M0; implementation in M2
- Date: 2026-08-03

## Context

Manifest, catalog, wave, configuration, and later graph data must be understood identically by Studio, CLI, packaging, runtime, tests, and documentation fixtures. Separate UI validation would drift from runtime behavior.

## Decision

ThroneForge uses JSON Schema 2020-12 for serialized data, with versioned schema IDs, embedded runtime resources, authoring/documentation copies, explicit migrations, and one validation facade exposed by `ThroneForge.Schemas`. Portable contract models remain free of JSON-library attributes unless a later ADR records a deliberate serialization decision. Stable sections reject unknown properties unless an explicit extension point exists. Validation returns structured issues with stable error codes and JSON locations instead of relying on exceptions for user input.

## Consequences

- Studio and CLI must call shared schema/migration services rather than reimplement rules.
- Every schema change requires fixtures and migration tests.
- Schema versioning remains independent from package/API versioning.
- M0 creates the boundary only; schema resources and validators are M2 work.

