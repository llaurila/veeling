# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

- Added `veeling update check` (manual version check) and `veeling update self` (channel-aware self-update guidance) as advisory-only update UX commands.
- Added OSS publication readiness documentation for release metadata, rollout operations, verification matrix, and upgrade guidance.
- Added natural-language CLI entrypoint `veeling ai "<intent>"` with alias `veeling ask`, mapping user intent to existing Veeling commands with a preview-and-confirmation flow.

### Changed

- Changed installation/update guidance to canonical public channels: `dotnet tool install --global veeling` and `dotnet tool update --global veeling`, with release-archive support.
- Changed release operations to require annotated SemVer tags (`vX.Y.Z`) with canonical version alignment and tag validation gating.
- `veeling config --global` now works reliably even when run outside a project directory.
- Added parser configuration keys `intent_parser_provider` and `intent_parser_model`, with fallback behavior when parser settings are not explicitly configured.

### Deprecated

- _None yet._

### Removed

- _None yet._

### Fixed

- Improved config test/runtime isolation around global config so test runs no longer mutate machine-global configuration.
- `veeling config` now returns a clear error when `--local` and `--global` are provided together.
- Improved intent parser robustness for provider outputs wrapped in markdown/prose and common schema variants, with safe fail-closed handling.

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
