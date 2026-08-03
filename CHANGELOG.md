# Changelog

All notable ThroneForge changes are documented here.

## Unreleased

- Fixed CI test-result preservation so every test project gets its own TRX artifact and the workflow verifies aggregate completeness before upload.
- Started M1 discovery task 1 with a local-only, metadata-based Thronefall installation inspector and sanitized fingerprint reports.

### Added

- M0 repository bootstrap and architecture skeleton.
- Architecture boundary tests for project and assembly references.
- M0 hardening branch with exact SDK pinning, repository dependency scans, CI TRX artifacts, and public project documentation.

### Not yet implemented

- Private Thronefall discovery evidence and game-facing runtime integration remain incomplete.
- Runtime integration, loaders, game API bindings, and custom-wave support remain unimplemented.
