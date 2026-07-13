# Release and Upgrade Guide

This guide is the public-facing reference for Veeling release expectations and safe upgrades.

## Supported release channels

- **Frontend/local-project preferred:** npm dev dependency (`@veeling/cli`)
- **Global machine default:** NuGet global tool (`veeling`)
- **Secondary:** standalone OS archives published with release artifacts

Canonical install/update commands are documented in `docs/installation.md`.

## Release notes expectations

Each released version `X.Y.Z` should have:

- an annotated SemVer tag `vX.Y.Z`,
- a matching changelog entry in `CHANGELOG.md`,
- release notes linked from the published release,
- matching release metadata in `release-metadata/latest.json`.

## Upgrade guidance

### NuGet channel

```bash
dotnet tool update --global veeling
```

If required, force NuGet.org source:

```bash
dotnet tool update --global veeling --add-source https://api.nuget.org/v3/index.json
```

### npm channel

Update to latest:

```bash
npm install -D @veeling/cli@latest
```

Run local binary:

```bash
npx veeling --version
```

### Archive channel

1. Download the new archive for your RID.
2. Verify checksum/signature.
3. Replace extracted directory with the new version.
4. Run `veeling --version`.

## Rollback guidance

### NuGet rollback

Reinstall previous known-good version explicitly:

```bash
dotnet tool install --global veeling --version <previous-version>
```

### npm rollback

Pin to previous known-good release:

```bash
npm install -D @veeling/cli@<previous-version>
```

If a bad version is published, maintainers should prefer non-destructive recovery:

- adjust dist-tags,
- publish a fixed follow-up release,
- deprecate the affected version with guidance.

### Archive rollback

Restore previous known-good extracted directory for your RID and verify:

```bash
veeling --version
```

## Failure recovery

- If checksum or signature validation fails, do not install the artifact.
- If feed/network lookup fails during update checks, command execution remains usable; retry later or run manual checks.
- Use `veeling update check` for manual advisory checks.

## Security and support routing

- Security reports: `SECURITY.md`
- User support and issue routing: `SUPPORT.md`

## Related references

- Installation and updates: `docs/installation.md`
- Versioning and release tags: `docs/versioning.md`
- Release metadata feed contract: `docs/release-metadata.md`
- Verification matrix: `docs/oss-verification-matrix.md`
- Phased rollout runbook: `docs/oss-phased-rollout-runbook.md`
