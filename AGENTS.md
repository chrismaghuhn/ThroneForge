# ThroneForge - Repository Instructions for Codex

## Source of truth

- Read `docs/THRONEFORGE_SPEC.md` before planning or changing architecture.
- Treat that specification, the current `PLAN.md`, and the current milestone in `STATUS.md` as authoritative.
- When requirements conflict, use this priority: user task > `AGENTS.md` > `docs/THRONEFORGE_SPEC.md` > `PLAN.md` > existing implementation.
- Do not silently reinterpret product requirements. Record necessary deviations in an ADR under `docs/adr/`.

## Operating model

- For any task larger than a focused bug fix, create or update `PLAN.md` before coding.
- Work milestone by milestone. Keep each change reviewable and do not start the next milestone while required checks fail.
- Update `STATUS.md` after every completed milestone with completed work, validation results, known limitations, and the next concrete task.
- Never invent Thronefall class names, methods, fields, scene names, IDs, or runtime behavior. Unknown game internals must be discovered from the user's legally obtained local installation and documented under `docs/discovery/`.
- When the game installation is unavailable, implement against abstractions and test fixtures; clearly mark game-facing work as unverified instead of guessing.

## Architecture boundaries

- Only `ThroneForge.GameAdapter.*` and `ThroneForge.Bootstrap.*` may reference game assemblies, Unity runtime assemblies, BepInEx, Harmony/HarmonyX, or reflection names for Thronefall internals.
- `ThroneForge.Contracts`, `ThroneForge.Schemas`, `ThroneForge.Packaging`, `ThroneForge.Logic`, and public API abstractions must remain independent of Thronefall implementation types.
- Studio and CLI must consume the same contracts, validators, migrations, and packaging services as the runtime. Do not duplicate schema logic in UI code.
- Content mods are data-only. Do not execute scripts, expressions, templates, or embedded binaries from a content mod.
- Code mods are fully trusted native .NET plugins and must be clearly separated, labeled, and opt-in.

## Engineering rules

- Use English for identifiers, public APIs, file names, logs, and code comments. User-facing UI text must be localizable.
- Enable nullable reference types, deterministic builds, analyzers, and warnings as errors for first-party projects.
- Pin the SDK in `global.json`; centralize NuGet versions in `Directory.Packages.props`; commit lock files where supported.
- Prefer small explicit abstractions over service-locator patterns, global mutable state, or reflection outside the adapter.
- Public IDs and serialized formats are compatibility contracts. Validate them strictly and add migrations before changing them.
- Use UTC timestamps and invariant-culture serialization in persisted data.
- Do not add a production dependency unless it removes substantial complexity or risk. Document the reason in the plan or an ADR.
- Never commit proprietary game binaries, copied game assets, decompiled source, user-specific game paths, secrets, or personal support bundles.

## Required validation

Run the relevant subset after every change and all of them before marking a milestone complete:

```bash
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
dotnet format --verify-no-changes
```

When present, also run:

```bash
dotnet test tests/ThroneForge.ArchitectureTests -c Release
dotnet test tests/ThroneForge.Contracts.Tests -c Release
```


For UI milestones, run the UI smoke-test procedure documented in `docs/testing/UI_SMOKE_TEST.md`. For game-facing milestones, run the clean-profile smoke test documented in `docs/testing/GAME_SMOKE_TEST.md` and attach sanitized logs.

## Definition of done

A task is not complete until:

- implementation matches the active milestone and acceptance criteria;
- tests cover success, failure, and boundary behavior;
- required commands pass;
- documentation and schemas match implementation;
- no proprietary files or local paths are staged;
- the final response lists changed files, validation commands and results, remaining risks, and any manual verification still required.
