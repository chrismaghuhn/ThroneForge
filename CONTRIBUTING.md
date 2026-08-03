# Contributing to ThroneForge

ThroneForge is currently at M0: an architecture skeleton only. Contributions must preserve the stable-core/adapter boundary and must not begin M1 discovery or game-facing work without an explicit task.

## Before opening a change

- Read [`AGENTS.md`](AGENTS.md), [`PLAN.md`](PLAN.md), [`STATUS.md`](STATUS.md), and the relevant ADRs.
- Do not add Thronefall binaries, copied assets, decompiled source, user-specific paths, secrets, or private support bundles.
- Do not invent Thronefall private types, members, IDs, scenes, or lifecycle behavior.
- Keep Unity, BepInEx, Harmony/HarmonyX, and game assembly references inside the permitted adapter/bootstrap boundary only.
- Add tests before implementation for behavior changes.

## Local checks

Run the commands in [`README.md`](README.md), including locked restore, formatting, Release build, and tests. Changes affecting the game adapter will also require the documented private smoke-test process once that exists.

## License status

TODO: The repository owner must select and approve a software license before accepting external contributions. Until then, please do not submit code intended for inclusion under an assumed license.

