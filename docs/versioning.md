---
title: Versioning Guide
category: process
---

# Veeling Versioning Guide

This guide defines Veeling's canonical version source and release version alignment rules.

## 1. SemVer policy

Veeling follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html):

```text
MAJOR.MINOR.PATCH
```

Current stability policy:

- Project remains in `0.x` until maintainers intentionally declare `1.0.0` stability.
- Never reuse or skip released version numbers.

## 2. Canonical version source (single source of truth)

The only canonical version declaration is in repository root `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <VeelingVersion>X.Y.Z</VeelingVersion>
    <Version>$(VeelingVersion)</Version>
  </PropertyGroup>
</Project>
```

### Why this is canonical

- `dotnet pack` uses MSBuild `Version` for NuGet package version.
- `dotnet publish` and assembly informational metadata resolve from the same `Version` value unless explicitly overridden.
- Release tooling can resolve version with:

```bash
dotnet msbuild Veeling.CLI/Veeling.CLI.csproj -nologo -getProperty:Version
```

- Archive tooling (`scripts/release-archives.sh`, `scripts/release-archives.ps1`) defaults to that resolved version when `--version` / `-Version` is not provided.

## 3. Alignment rules (no drift)

For any release `X.Y.Z`, all of the following must match exactly:

1. `Directory.Build.props` `<VeelingVersion>` = `X.Y.Z`
2. Release tag = `vX.Y.Z`
3. Changelog release heading/section references `X.Y.Z`
4. Published package version = `X.Y.Z`
5. Release notes describe the same release `X.Y.Z`

If any one differs, release is considered invalid and must be corrected before publication.

## 4. Changelog workflow (Keep a Changelog)

`CHANGELOG.md` is mandatory and must follow this structure:

1. Top section is always `## [Unreleased]`.
2. `Unreleased` contains these standard subsections (use `_None yet._` when empty):
   - `Added`
   - `Changed`
   - `Deprecated`
   - `Removed`
   - `Fixed`
   - `Security`
3. Released entries are formatted as:

```markdown
## [X.Y.Z] - YYYY-MM-DD
```

4. New release sections are inserted directly below `Unreleased`.

### Release cut procedure (changelog-first)

For a release version `X.Y.Z`:

1. Ensure all pending changes are captured under `## [Unreleased]`.
2. Update canonical version source in `Directory.Build.props` to `X.Y.Z`.
3. Move `Unreleased` content into a new release section `## [X.Y.Z] - YYYY-MM-DD`.
4. Re-create a fresh `## [Unreleased]` section with all six standard subsections set to `_None yet._`.
5. Create annotated SemVer tag `vX.Y.Z` on that release commit.
6. Validate tag policy:

```bash
bash scripts/validate-release-tag.sh --tag vX.Y.Z
```

7. Publish package/artifacts from that tagged commit.
8. Update `release-metadata/latest.json` for the released version (see `docs/release-metadata.md`).

## 5. Maintainer procedure (version bump)

1. Update `Directory.Build.props` `<VeelingVersion>`.
2. Verify resolved version:

```bash
dotnet msbuild Veeling.CLI/Veeling.CLI.csproj -nologo -getProperty:Version
```

3. Execute changelog release cut procedure (section 4) for that version.
4. Create annotated tag `vX.Y.Z` on the release commit.
5. Validate release-tag compliance before publishing:

```bash
bash scripts/validate-release-tag.sh --tag vX.Y.Z
```

Validation fails if the tag is not `vX.Y.Z`, is lightweight (non-annotated), does not point to `HEAD`, or does not match canonical version.

6. Publish package/artifacts from that tagged commit.

## 6. Verification commands

Check canonical value:

```bash
dotnet msbuild Veeling.CLI/Veeling.CLI.csproj -nologo -getProperty:Version
```

Check changelog structure and top heading:

```bash
rg "^## \[(Unreleased|[0-9]+\.[0-9]+\.[0-9]+)\]" CHANGELOG.md
```

Check package version output:

```bash
dotnet pack Veeling.CLI/Veeling.CLI.csproj -c Release -o ./artifacts/nupkg
```

Build archives from validated tag (mandatory tag input):

```bash
bash scripts/release-archives.sh --tag vX.Y.Z --rids linux-x64 --output ./artifacts/releases-smoke
```

Check release-tag compliance directly:

```bash
bash scripts/validate-release-tag.sh --tag vX.Y.Z
```

## 7. Scope notes

- This story slice establishes canonical version source and SemVer annotated-tag enforcement.
- Changelog operating details are included in this slice (S3.T3).
