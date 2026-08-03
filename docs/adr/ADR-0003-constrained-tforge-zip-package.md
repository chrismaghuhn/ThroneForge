# ADR-0003: `.tforge` as a constrained ZIP-compatible package

- Status: Accepted for M0; implementation in M3
- Date: 2026-08-03

## Context

Creators need a portable package that Studio, CLI, and runtime can validate consistently. A ZIP-compatible archive is broadly supported, but unrestricted extraction creates path traversal, decompression bomb, executable-content, and reproducibility risks.

## Decision

The `.tforge` format is a ZIP-compatible archive with a root `manifest.json`, normalized forward-slash paths, optional content/logic/localization/config/assets sections, and `integrity.json` for SHA-256/size metadata. M3 will enforce the specification's path, file count, size, compression ratio, JSON depth, duplicate-entry, symlink/reparse-point, and executable-content rules. Package installation stages validation and commits atomically; installed packages remain immutable and profile state is separate.

## Consequences

- Existing ZIP tooling can inspect packages, while the reader remains stricter than a general archive extractor.
- Reproducible builds require controlled path ordering, UTF-8/LF text normalization, and a documented archive timestamp policy.
- M0 records the format without implementing a package reader or copying game assets.

