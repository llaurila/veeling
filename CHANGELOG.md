# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

- Added real-time `veeling translate` progress streaming with structured progress events and live `[n/total]` field updates in CLI output.

### Changed

- _None yet._

### Deprecated

- _None yet._

### Removed

- _None yet._

### Fixed

- _None yet._

### Security

- _None yet._

## [0.4.2] - 2026-07-03

### Added

- Added `veeling translate --changed` to include retranslation candidates whose master/source content has drifted since the last translation.
- Added `veeling update check` (manual version check) and `veeling update self` (channel-aware self-update guidance) as advisory-only update UX commands.
- Added OSS publication readiness documentation for release metadata, rollout operations, verification matrix, and upgrade guidance.
- Added natural-language CLI entrypoint `veeling ai "<intent>"` with alias `veeling ask`, mapping user intent to existing Veeling commands with a preview-and-confirmation flow.

### Changed

- `veeling translate` now preserves missing-only behavior by default, while `--changed` explicitly enables missing + drifted target selection from master-source drift tracking.
- Changed installation/update guidance to canonical public channels: `dotnet tool install --global veeling` and `dotnet tool update --global veeling`, with release-archive support.
- Changed release operations to require annotated SemVer tags (`vX.Y.Z`) with canonical version alignment and tag validation gating.
- Hardened release orchestration after public bootstrap with smoke-path/script fixes, dry-run tag-policy consistency, and robust annotated-tag detection in CI.
- `veeling config --global` now works reliably even when run outside a project directory.
- Added parser configuration keys `intent_parser_provider` and `intent_parser_model`, with fallback behavior when parser settings are not explicitly configured.
- Documented and operationalized private-first dual-repository governance (private continuity repo authoritative; public OSS repo as sanitized projection), including sync gates, audit evidence expectations, and aligned agent/skill guidance.

### Deprecated

- _None yet._

### Removed

- _None yet._

### Fixed

- Improved config test/runtime isolation around global config so test runs no longer mutate machine-global configuration.
- `veeling config` now returns a clear error when `--local` and `--global` are provided together.
- Improved intent parser robustness for provider outputs wrapped in markdown/prose and common schema variants, with safe fail-closed handling.
- Verified first public launch outcome end-to-end (`v0.4.1`): successful release workflow execution, GitHub release publication, and NuGet package availability/installability for `veeling`.

### Security

- Added release integrity controls for checksums, optional signing policies, provenance attestations, and optional SBOM generation in release workflows.

## [0.4.1] - 2026-06-26

### Added

- Added a guided `veeling onboard` command that walks users through provider selection, model selection (including an `Other` path), credential capture, and global config persistence for OpenAI, Gemini, and Claude.

### Changed

- Updated onboarding flow to verify AI connectivity immediately after setup via the runtime provider abstraction, with concise success/failure feedback and no secret-value leakage.

### Fixed

- Hardened translation response handling to fail before writes on malformed/partial payloads, with bounded diagnostics and deterministic CLI exit codes for parse/provider/auth/missing-source failures (story `0003`).
- Unified translation provider auth-failure classification across provider initialization and execution so equivalent failures now consistently map to auth vs provider exit-code semantics (story `0005`).
