# Release Metadata Contract (Update Check)

This document defines the canonical release metadata feed for Veeling version checks.

## Canonical hosting path

Primary metadata file (repository path):

- `release-metadata/latest.json`

Canonical hosted URL for runtime clients:

- `https://raw.githubusercontent.com/llaurila/veeling/main/release-metadata/latest.json`

Contract schema path:

- `release-metadata/schema.v1.json`

Notes:

- This feed is version-controlled and published with repository updates.
- If unavailable, consumers should use GitHub Releases API fallback (to be implemented in S4.T2).

## Contract version

- `schema_version: "1.0"` is required.
- Breaking schema changes must publish a new schema version (for example `2.0`) and a matching schema file.

## Required payload shape

Top-level fields:

- `schema_version` (string, required)
- `generated_at` (ISO-8601 UTC datetime string, required)
- `source.repository` (URI, required)
- `source.releases_url` (URI, required)
- `channels.stable` (release object, required)
- `channels.prerelease` (release object or `null`, required)

Each release object must include:

- `version` (SemVer, no `v` prefix)
- `tag` (`vX.Y.Z` SemVer tag format)
- `published_at` (ISO-8601 UTC datetime)
- `release_url` (URL to release page)
- `notes_url` (URL to release notes page)
- `changelog_url` (URL to changelog)
- `compatibility.minimum_cli_version` (SemVer or `null`)
- `compatibility.notes` (string)

Minimum required data from story scope is covered by:

- latest stable + optional prerelease
- release URL
- published date
- compatibility notes

## Example (`latest.json`)

```json
{
  "schema_version": "1.0",
  "generated_at": "2026-06-26T00:00:00Z",
  "source": {
    "repository": "https://github.com/llaurila/veeling",
    "releases_url": "https://github.com/llaurila/veeling/releases"
  },
  "channels": {
    "stable": {
      "version": "0.4.1",
      "tag": "v0.4.1",
      "published_at": "2026-06-26T00:00:00Z",
      "release_url": "https://github.com/llaurila/veeling/releases/tag/v0.4.1",
      "notes_url": "https://github.com/llaurila/veeling/releases/tag/v0.4.1",
      "changelog_url": "https://github.com/llaurila/veeling/blob/main/CHANGELOG.md",
      "compatibility": {
        "minimum_cli_version": null,
        "notes": "No compatibility floor declared yet."
      }
    },
    "prerelease": null
  }
}
```

## Publication and update expectations

When cutting a release `X.Y.Z`:

1. Complete version/changelog/tag flow from `docs/versioning.md`.
2. Update `release-metadata/latest.json`:
   - set `generated_at` to current UTC timestamp,
   - set `channels.stable` to the new release payload,
   - update `channels.prerelease` (release object or `null`).
3. Ensure `version` equals canonical MSBuild version from `Directory.Build.props`.
4. Ensure `tag` is the same validated annotated tag (`vX.Y.Z`).
5. Ensure URLs point to the exact release/tag and current changelog.
6. Validate JSON shape against `release-metadata/schema.v1.json`.

Expected client behavior contract for S4.T2:

- Parse only known fields (`additionalProperties: false` in schema).
- Treat invalid/unavailable metadata as non-fatal and continue command execution.
- Prefer `channels.stable` for default advisory checks.
- Use `channels.prerelease` only when prerelease checks are explicitly enabled by user policy.

## Runtime retrieval/caching contract (S4.T2 baseline)

Current implementation baseline in application layer:

- Service: `UpdateCheckApplicationService`
- Metadata client: `ReleaseMetadataClient`
- Cache store: `FileSystemUpdateCheckCache` at `~/.veeling/update-check-cache.json`

Behavior:

- Timeout-bounded HTTP fetch (default `2s`, configurable via `UpdateCheck:TimeoutSeconds`).
- Cache TTL default `24h` (`UpdateCheck:CacheTtlHours`).
- If cached result is fresh, no network call is made.
- On timeout/network/parse failures:
  - stale cache available -> return cached metadata (non-fatal),
  - no cache -> return non-fatal check failure with no metadata.
- Errors never fail command execution path; update checks are advisory and offline-safe.

Configuration keys (appsettings):

- `UpdateCheck:MetadataUrl`
- `UpdateCheck:TimeoutSeconds`
- `UpdateCheck:CacheTtlHours`

## CLI advisory UX controls (S4.T3)

- Automatic advisory check runs in background by default and is non-fatal.
- Global opt-out via config:

```bash
veeling config --global --key update_check_enabled --value false
```

- Re-enable:

```bash
veeling config --global --key update_check_enabled --value true
```

- Manual check path:

```bash
veeling update check
veeling update check --prerelease
```

- User-controlled self-update guidance (never mutates system):

```bash
veeling update self
veeling update self --source nuget
veeling update self --source archive
```

If install source cannot be determined, CLI prints safe advisory instructions for both NuGet and archive paths.
