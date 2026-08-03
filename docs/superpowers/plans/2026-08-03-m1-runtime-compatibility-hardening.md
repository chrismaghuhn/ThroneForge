# M1 Task 2 Hardening Plan

## Scope

Harden the metadata-only runtime compatibility investigation on `agent/m1-runtime-compatibility-hardening`. Do not install or execute BepInEx, inspect game methods, add game-facing references, or begin Task 3.

## Execution order

1. Capture the review findings in tests: fingerprint binding and no-write mismatch behavior; readiness blockers; evidence-specific TFM confidence; and independent evidence categories.
2. Extract the existing Task 1 fingerprint inputs into one non-writing installation snapshot service. Make both `inspect` and `runtime-compatibility` consume it without changing fingerprint-v1 normalization.
3. Verify the supplied runtime fingerprint against the snapshot before any runtime report write. Keep the Task 1 report untouched on mismatch.
4. Add structured target-framework assessment and smoke-test readiness. Select the provisional candidate independently from readiness, then evaluate loader indicators and compatibility blockers.
5. Carry structured conflict, missing, limitation, and warning issues into the runtime report, especially bounded `globalgamemanagers` limitations.
6. Update the fingerprint-specific report and repository documentation only after local checks pass; do not claim the new hosted result early.
7. Run exact validation, push the branch, inspect both hosted runners and all TRX artifacts, then record the new branch-head evidence. Stop before Task 3.

## Review checkpoints

- Shared snapshot preserves the existing deterministic fingerprint for the known synthetic fixture.
- A mismatched fingerprint fails before output creation and never writes the Task 1 report.
- A clean supported Mono/x64/netstandard profile may be ready while the same profile with any loader indicator is blocked.
- The current private evidence reports `netstandard2.1 candidate`, `Medium`, with the Unity-version inference basis.
- Bounded-read messages are reported as inspection limitations when evidence was found in the inspected prefix.
- M1 remains incomplete and Task 3 remains the next narrowly scoped investigation.
