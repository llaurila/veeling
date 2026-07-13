# Veeling Installation and Updates

This guide defines the official installation and update paths for Veeling.

## Primary frontend channel: npm local dev dependency

Install in a frontend project:

```bash
npm install -D @veeling/cli
```

Run via local binary shims:

```bash
npx veeling --version
```

Supported npm package payloads:

- `@veeling/cli` (meta package)
- `@veeling/cli-win32-x64`
- `@veeling/cli-linux-x64`
- `@veeling/cli-darwin-x64`
- `@veeling/cli-darwin-arm64`

Notes:

- npm is the preferred install path for frontend/local-project usage.
- The package family contains prebuilt binaries; install-time downloaders are intentionally avoided.

## Default global channel (official): NuGet global tool

Install:

```bash
dotnet tool install --global veeling
```

Update:

```bash
dotnet tool update --global veeling
```

Verify installed version:

```bash
veeling --version
```

### Notes

- Veeling is published as a .NET global tool package (`veeling`).
- Installation and updates require a compatible .NET SDK/runtime environment.
- If your machine uses multiple package feeds and cannot resolve the package from NuGet.org, provide the source explicitly:

```bash
dotnet tool install --global veeling --add-source https://api.nuget.org/v3/index.json
```

## Secondary channel (planned): OS release archives

Standalone archives for Windows, macOS, and Linux are provided as the secondary channel.

Maintainers generate archives with a mandatory annotated SemVer tag:

```bash
bash scripts/release-archives.sh --tag v<X.Y.Z>
```

On Windows-native PowerShell:

```powershell
./scripts/release-archives.ps1 -Tag v<X.Y.Z>
```

The archive scripts fail fast when the tag is non-compliant (`vX.Y.Z`), lightweight (not annotated), not on `HEAD`, or mismatched against canonical MSBuild version.

Both scripts now include NuGet package checksum coverage in the same `SHA256SUMS` file (archive artifacts + `veeling.<version>.nupkg`).

### Signing policy and channels

Supported signing channels:

- **NuGet package signing:** `dotnet nuget sign` over `veeling.<version>.nupkg`.
- **Archive signing:** detached armored GPG signatures (`.asc`) per archive.

Signing mode is explicit and configurable in release scripts:

- `off` — do not attempt signing.
- `optional` (default) — sign when config exists; warn when missing.
- `required` — fail release script when signing config/tooling is missing.

Integrity generation policies now also cover provenance and SBOM generation:

- `--provenance-mode` / `-ProvenanceMode`: `off|optional|required`
- `--sbom-mode` / `-SbomMode`: `off|optional|required`
- `--sbom-tool` / `-SbomTool`: `auto|syft`

#### Bash

```bash
bash scripts/release-archives.sh --tag v<X.Y.Z> --signing-mode required
```

Preflight only:

```bash
bash scripts/release-archives.sh --signing-mode required --validate-signing-config
```

Full integrity preflight (signing + provenance + SBOM):

```bash
bash scripts/release-archives.sh --signing-mode required --provenance-mode required --sbom-mode optional --validate-integrity-config
```

#### PowerShell

```powershell
./scripts/release-archives.ps1 -Tag v<X.Y.Z> -SigningMode required
```

Preflight only:

```powershell
./scripts/release-archives.ps1 -SigningMode required -ValidateSigningConfig
```

Full integrity preflight:

```powershell
./scripts/release-archives.ps1 -SigningMode required -ProvenanceMode required -SbomMode optional -ValidateIntegrityConfig
```

#### Signing configuration (maintainer-provided; no secrets in repo)

Environment variables used by both scripts:

- `VEELING_NUGET_SIGN_CERT_PATH` — path to signing `.pfx` certificate.
- `VEELING_NUGET_SIGN_CERT_PASSWORD` — certificate password (optional env var).
- `VEELING_NUGET_SIGN_TIMESTAMP_URL` — RFC3161 timestamp URL (optional).
- `VEELING_GPG_KEY_ID` — GPG key ID used for archive detached signatures.

If signing prerequisites are absent:

- `optional`: script continues and prints warnings.
- `required`: script exits non-zero.

### Provenance / attestation

Release scripts generate in-toto style provenance statement file:

- `provenance.intoto.json`

Policy and behavior:

- `off`: skip provenance generation.
- `optional`: generate when git metadata is available; warn if unavailable.
- `required`: fail if provenance cannot be generated.

Provenance captures release tag/version parameters and subject digests for generated outputs.

### SBOM generation (optional)

When SBOM tooling is available/configured, scripts generate:

- `sbom.<version>.cdx.json` (CycloneDX JSON)

Current supported SBOM tool:

- `syft`

Behavior:

- `off`: skip SBOM generation.
- `optional`: generate if `syft` available; warn otherwise.
- `required`: fail if SBOM tool/output unavailable.

Default RIDs:

- `win-x64`
- `linux-x64`
- `osx-x64`
- `osx-arm64`

### Archive shape

- `veeling-<version>-win-x64.zip`
- `veeling-<version>-linux-x64.tar.gz`
- `veeling-<version>-osx-x64.tar.gz`
- `veeling-<version>-osx-arm64.tar.gz`

Archive names are deterministic and always follow:

- `veeling-<version>-<rid>.<ext>`

Each archive release includes:

- SHA256 checksum file (`SHA256SUMS`)
- SHA256 entries for all generated archives and the matching NuGet package (`veeling.<version>.nupkg`)
- detached archive signatures (`*.asc`) when signing is configured/enabled
- provenance attestation (`provenance.intoto.json`) when enabled
- SBOM (`sbom.<version>.cdx.json`) when enabled/tooling available
- release notes link
- extraction + path setup instructions

### Archive install and update guidance

1. Download the archive for your OS/RID and the matching `SHA256SUMS` file.
2. Verify checksum before extraction.
3. Extract archive and add the extracted directory to your shell `PATH`.
4. Run `veeling --version` to confirm installation.

To update, download the newer archive for the same RID, verify checksum, replace the previous extracted folder, and re-run `veeling --version`.

### Checksum verification examples

Linux/macOS:

```bash
sha256sum -c SHA256SUMS
```

Verify a specific package/archive entry:

```bash
grep "veeling-<version>-linux-x64.tar.gz" SHA256SUMS && grep "veeling.<version>.nupkg" SHA256SUMS
```

PowerShell:

```powershell
Get-FileHash .\veeling-<version>-win-x64.zip -Algorithm SHA256
```

Verify package hash matches `SHA256SUMS` entry:

```powershell
$hash = (Get-FileHash .\veeling.<version>.nupkg -Algorithm SHA256).Hash.ToLowerInvariant();
Select-String -Path .\SHA256SUMS -Pattern "veeling\.<version>\.nupkg" | ForEach-Object { $_.Line }
```

### Signature verification examples

Verify NuGet package signature:

```bash
dotnet nuget verify ./artifacts/nupkg/veeling.<version>.nupkg
```

Verify archive detached signature:

```bash
gpg --verify ./artifacts/releases/veeling-<version>-linux-x64.tar.gz.asc ./artifacts/releases/veeling-<version>-linux-x64.tar.gz
```

Inspect provenance subject list:

```bash
python -c "import json; d=json.load(open('./artifacts/releases/provenance.intoto.json','r',encoding='utf-8')); print(len(d.get('subject',[])))"
```

Inspect SBOM document type (CycloneDX):

```bash
python -c "import json; d=json.load(open('./artifacts/releases/sbom.<version>.cdx.json','r',encoding='utf-8')); print(d.get('bomFormat'))"
```

### Signing handoff (release operations)

Archive signing is intentionally not automated in-repo yet, because it requires maintainer-managed secrets/certificates. Release operators must add signing in the publish pipeline and attach signatures to release assets once the signing material and key-management workflow are approved.

## Maintainer release preparation (NuGet primary path)

Use these commands to validate package metadata and produce publishable artifacts:

```bash
dotnet restore Veeling.CLI/Veeling.CLI.csproj
dotnet pack Veeling.CLI/Veeling.CLI.csproj -c Release -o ./artifacts/nupkg
dotnet nuget push ./artifacts/nupkg/veeling.*.nupkg --source https://api.nuget.org/v3/index.json
```

For first-time publication, verify the package metadata fields in `Veeling.CLI/Veeling.CLI.csproj` are populated and valid before `dotnet nuget push`.

Preferred authentication model: NuGet Trusted Publishing via GitHub Actions OIDC.

Fallback only (if explicitly authorized):

```bash
dotnet nuget push ./artifacts/nupkg/veeling.*.nupkg --api-key <NUGET_API_KEY> --source https://api.nuget.org/v3/index.json
```

### Maintainer npm release preparation

Npm package payloads are derived from the same release archive outputs and canonical version source (`Directory.Build.props`).

Local packaging rehearsal (no publish):

```powershell
./scripts/stage-npm-artifacts.ps1 -Version v<X.Y.Z> -ArchiveInput ./artifacts/releases -NpmRoot ./npm -Clean
```

Then pack workspace packages:

```bash
npm --prefix ./npm pack --workspaces --include-workspace-root=false --pack-destination ./artifacts/npm
```

Content safety validation (deny private-only paths from tarballs):

```bash
node ./scripts/verify-npm-tarball-contents.mjs ./artifacts/npm/*.tgz
```

Publish policy:

- Trusted publishing + provenance is primary.
- Token fallback is break-glass only, and may be authorized **only by `vincentlaurila`** for **one release incident**.
- No npm credentials are stored in-repo.

### Tag-driven release orchestration (GitHub Actions)

Release orchestration workflow: `.github/workflows/release-tag-orchestration.yml`

Triggers:

- `push` tag matching `vX.Y.Z` (release mode)
- manual `workflow_dispatch` with:
  - `tag` (required)
  - `dry_run` (default `true`)

Workflow guardrails before publish actions:

- Validates tag policy using `scripts/validate-release-tag.sh`
- Runs `dotnet restore`, `dotnet build`, `dotnet test`
- Runs integrity preflight with `scripts/release-archives.sh --validate-integrity-config`

Release smoke tests (gating publish jobs):

- **NuGet global tool path (clean-machine):**
  - Isolated HOME/CLI/NuGet paths on ephemeral runner
  - `dotnet tool install --global veeling` from workflow-generated release bundle source
  - `dotnet tool update --global veeling` against same isolated source
  - strict version assertions via `veeling --version`
- **Archive path (practical CI matrix):**
  - Ubuntu `linux-x64` archive extract + version assertion + replacement update simulation
  - Windows `win-x64` archive extract + `veeling.exe --version` assertion + replacement update simulation

Publish jobs (`GitHub Release` and `NuGet push`) depend on all smoke jobs passing.

Artifact generation uses existing release script path and emits:

- release archives for supported RIDs
- NuGet package (`veeling.<version>.nupkg`)
- `SHA256SUMS`
- optional signing outputs (`*.asc`)
- provenance (`provenance.intoto.json`)
- optional SBOM (`sbom.<version>.cdx.json`)

NuGet publish safety:

- publish step runs only in non-dry-run mode
- workflow requests OIDC token permission (`id-token: write`) for Trusted Publishing
- workflow first attempts `dotnet nuget push` without API key (Trusted Publishing path)
- if Trusted Publishing fails and `NUGET_API_KEY` is configured, workflow attempts explicit fallback push with API key
- if Trusted Publishing fails and no fallback key is configured, workflow fails closed

Fallback secret for publish contingency:

- `NUGET_API_KEY`

Optional env vars for signing/integrity enrichment are the same as documented above for release scripts.

### Canonical version source

Veeling uses one canonical version declaration at repository root:

- `Directory.Build.props` → `<VeelingVersion>`

This value flows into MSBuild `<Version>` for all projects, and is consumed by package/archive flows (`dotnet pack`, `dotnet publish`, `dotnet msbuild -getProperty:Version`).

Release operators should bump version in exactly one place (`Directory.Build.props`) and then align release tag (`vX.Y.Z`) and changelog/release notes to the same value. Before publish, run:

```bash
bash scripts/validate-release-tag.sh --tag v<X.Y.Z>
```

Changelog release cut must follow Keep a Changelog with a persistent `## [Unreleased]` top section and released headings in format `## [X.Y.Z] - YYYY-MM-DD` (see `docs/versioning.md`).

Release metadata feed for update checks is published at `release-metadata/latest.json` (canonical runtime URL documented in `docs/release-metadata.md`).

## Maintainer archive release preparation (secondary path)

Build archives and checksums:

```bash
bash scripts/release-archives.sh --tag v<X.Y.Z>
```

Optional custom output dirs:

```bash
bash scripts/release-archives.sh --tag v<X.Y.Z> --output ./artifacts/releases --nupkg-output ./artifacts/nupkg
```

PowerShell equivalent:

```powershell
./scripts/release-archives.ps1 -Tag v<X.Y.Z>
```

PowerShell custom output dirs:

```powershell
./scripts/release-archives.ps1 -Tag v<X.Y.Z> -OutputDir ./artifacts/releases -NupkgDir ./artifacts/nupkg
```

This creates archives and `SHA256SUMS` under `./artifacts/releases` by default, and `veeling.<version>.nupkg` under `./artifacts/nupkg`; `SHA256SUMS` contains entries for both classes.

Checksum entries are written in deterministic filename-sorted order to reduce release-output drift between runs.

## Contributor-only local packaging smoke tests

Repository scripts (`install.sh`, `update.sh`, `Install.ps1`, `Update.ps1`) remain available for local build/install smoke checks and should not be presented as the primary public installation path.

## Update advisory and self-update guidance

- Automatic update advisories are non-blocking and non-fatal.
- Disable advisory checks globally:

```bash
veeling config --global --key update_check_enabled --value false
```

- Manual check:

```bash
veeling update check
```

- Self-update guidance (user-controlled, no silent mutation):

```bash
veeling update self
```

NuGet users are instructed to run:

```bash
dotnet tool update --global veeling
```
