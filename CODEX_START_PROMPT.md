# Start prompt for Codex

Use this prompt from the repository root after placing `AGENTS.md` and `docs/THRONEFORGE_SPEC.md` in the repository.

---

Read `AGENTS.md` and `docs/THRONEFORGE_SPEC.md` completely before making changes.

Your goal is to build ThroneForge as a maintainable, Forge-like mod SDK and no-code/low-code authoring ecosystem for Thronefall. The specification is intentionally strict about architecture boundaries, validation, compatibility, security, and not inventing game internals.

Begin in plan mode. Inspect the current repository and then:

1. Create `PLAN.md` with milestone-by-milestone implementation steps derived from section 25 of the specification.
2. Create `STATUS.md` with the project state, assumptions, blockers, and the next executable task.
3. Create the initial ADRs required by the specification, including the runtime-target decision, package-format decision, and adapter boundary.
4. Implement only Milestone M0 (repository bootstrap and architecture skeleton) unless M0 is already complete.
5. Add automated architecture tests that enforce the dependency boundaries before implementing game-specific functionality.
6. Run every validation command required by `AGENTS.md`, fix all failures, and update `STATUS.md` with exact results.

Hard constraints:

- Do not invent Thronefall types, methods, fields, IDs, scenes, or lifecycle behavior.
- Do not download, commit, or redistribute game binaries or copied assets.
- Do not put Harmony, BepInEx, Unity, or game references into contracts, schemas, packaging, logic, Studio domain models, or CLI domain models.
- Do not implement the visual node editor before the custom-wave vertical slice is working.
- Do not claim a game-facing feature works without a documented clean-profile smoke test against a locally installed game build.
- Keep changes reviewable and stop to repair failed checks before moving forward.

Done for this task means M0 satisfies every M0 acceptance criterion in the specification, all automated checks pass, and the repository contains a clear plan for M1 without pretending that discovery work has already been completed.

At the end, report:

- files created or changed;
- architectural decisions made;
- commands executed and their results;
- assumptions that remain unverified;
- the exact first task for M1.
