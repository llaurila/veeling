#!/usr/bin/env bash

set -euo pipefail

PROJECT_PATH="Veeling.CLI/Veeling.CLI.csproj"
OUTPUT_DIR="./artifacts/releases"
NUPKG_DIR="./artifacts/nupkg"
TAG=""
VERSION=""
SIGNING_MODE="optional"
PROVENANCE_MODE="optional"
SBOM_MODE="optional"
SBOM_TOOL="auto"
VALIDATE_SIGNING_CONFIG=false
VALIDATE_INTEGRITY_CONFIG=false
RIDS=("win-x64" "linux-x64" "osx-x64" "osx-arm64")

usage() {
  cat <<'EOF'
Usage: scripts/release-archives.sh [options]

Build Veeling release archives for one or more RIDs and emit SHA256 checksums.

Options:
  --tag <vX.Y.Z>          Annotated SemVer release tag to build from (required).
  --output <path>         Output directory (default: ./artifacts/releases).
  --nupkg-output <path>   NuGet package output directory (default: ./artifacts/nupkg).
  --signing-mode <mode>   Signing policy: off|optional|required (default: optional).
  --provenance-mode <mode>
                          Provenance policy: off|optional|required (default: optional).
  --sbom-mode <mode>      SBOM policy: off|optional|required (default: optional).
  --sbom-tool <tool>      SBOM tool: auto|syft (default: auto).
  --validate-signing-config
                          Validate signing tooling/env and exit (no build).
  --validate-integrity-config
                          Validate signing/provenance/SBOM tooling/env and exit (no build).
  --project <path>        Project path (default: Veeling.CLI/Veeling.CLI.csproj).
  --rids <rid1,rid2>      Comma-separated RIDs (default: win-x64,linux-x64,osx-x64,osx-arm64).
  --help                  Show this help and exit.

Examples:
  scripts/release-archives.sh --tag v0.4.1
  scripts/release-archives.sh --tag v0.4.1 --rids win-x64,linux-x64 --output ./dist/releases
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag)
      TAG="$2"
      shift 2
      ;;
    --output)
      OUTPUT_DIR="$2"
      shift 2
      ;;
    --nupkg-output)
      NUPKG_DIR="$2"
      shift 2
      ;;
    --signing-mode)
      SIGNING_MODE="${2,,}"
      shift 2
      ;;
    --provenance-mode)
      PROVENANCE_MODE="${2,,}"
      shift 2
      ;;
    --sbom-mode)
      SBOM_MODE="${2,,}"
      shift 2
      ;;
    --sbom-tool)
      SBOM_TOOL="${2,,}"
      shift 2
      ;;
    --validate-signing-config)
      VALIDATE_SIGNING_CONFIG=true
      shift 1
      ;;
    --validate-integrity-config)
      VALIDATE_INTEGRITY_CONFIG=true
      shift 1
      ;;
    --project)
      PROJECT_PATH="$2"
      shift 2
      ;;
    --rids)
      IFS=',' read -r -a RIDS <<< "$2"
      shift 2
      ;;
    --help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

validate_mode() {
  local mode_name="$1"
  local value="$2"
  if [[ "$value" != "off" && "$value" != "optional" && "$value" != "required" ]]; then
    echo "Invalid ${mode_name} '$value'. Expected: off|optional|required" >&2
    exit 2
  fi
}

validate_mode "--signing-mode" "$SIGNING_MODE"
validate_mode "--provenance-mode" "$PROVENANCE_MODE"
validate_mode "--sbom-mode" "$SBOM_MODE"

if [[ "$SBOM_TOOL" != "auto" && "$SBOM_TOOL" != "syft" ]]; then
  echo "Invalid --sbom-tool '$SBOM_TOOL'. Expected: auto|syft" >&2
  exit 2
fi

require_or_warn() {
  local mode="$1"
  local message="$2"
  if [[ "$mode" == "required" ]]; then
    echo "$message" >&2
    exit 1
  fi

  if [[ "$mode" == "optional" ]]; then
    echo "[signing] WARN: $message" >&2
  fi
}

signing_prereq_nuget_ok() {
  [[ -n "${VEELING_NUGET_SIGN_CERT_PATH:-}" ]] && [[ -f "${VEELING_NUGET_SIGN_CERT_PATH}" ]]
}

signing_prereq_archive_ok() {
  command -v gpg >/dev/null 2>&1 && [[ -n "${VEELING_GPG_KEY_ID:-}" ]]
}

validate_signing_config() {
  if [[ "$SIGNING_MODE" == "off" ]]; then
    echo "[signing] Signing disabled by policy (--signing-mode=off)."
    return 0
  fi

  if signing_prereq_nuget_ok; then
    echo "[signing] NuGet signing configured."
  else
    require_or_warn "$SIGNING_MODE" "NuGet signing not configured. Set VEELING_NUGET_SIGN_CERT_PATH (existing .pfx)."
  fi

  if signing_prereq_archive_ok; then
    echo "[signing] Archive signing configured (gpg key: ${VEELING_GPG_KEY_ID})."
  else
    require_or_warn "$SIGNING_MODE" "Archive signing not configured. Install gpg and set VEELING_GPG_KEY_ID."
  fi
}

provenance_prereq_ok() {
  git rev-parse --is-inside-work-tree >/dev/null 2>&1
}

detect_sbom_tool() {
  if [[ "$SBOM_TOOL" == "syft" ]]; then
    echo "syft"
    return 0
  fi

  if command -v syft >/dev/null 2>&1; then
    echo "syft"
    return 0
  fi

  echo ""
}

validate_integrity_config() {
  validate_signing_config

  if [[ "$PROVENANCE_MODE" == "off" ]]; then
    echo "[provenance] Disabled by policy (--provenance-mode=off)."
  elif provenance_prereq_ok; then
    echo "[provenance] Repository metadata available."
  else
    require_or_warn "$PROVENANCE_MODE" "Provenance generation requires git repository metadata."
  fi

  if [[ "$SBOM_MODE" == "off" ]]; then
    echo "[sbom] Disabled by policy (--sbom-mode=off)."
  else
    local tool
    tool="$(detect_sbom_tool)"
    if [[ -n "$tool" ]]; then
      echo "[sbom] Tool available: $tool"
    else
      require_or_warn "$SBOM_MODE" "No SBOM tool available (supported: syft)."
    fi
  fi
}

if [[ "$VALIDATE_SIGNING_CONFIG" == "true" ]]; then
  validate_signing_config
  exit 0
fi

if [[ "$VALIDATE_INTEGRITY_CONFIG" == "true" ]]; then
  validate_integrity_config
  exit 0
fi

if [[ -z "$TAG" ]]; then
  echo "Missing required --tag option." >&2
  usage >&2
  exit 2
fi

bash "scripts/validate-release-tag.sh" --tag "$TAG" --project "$PROJECT_PATH"
VERSION="${TAG#v}"

if [[ ${#RIDS[@]} -eq 0 ]]; then
  echo "At least one RID must be provided." >&2
  exit 1
fi

mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"
mkdir -p "$NUPKG_DIR"
NUPKG_DIR="$(cd "$NUPKG_DIR" && pwd)"
tmp_root="$(mktemp -d)"
checksum_file="$OUTPUT_DIR/SHA256SUMS"
trap 'rm -rf "$tmp_root"' EXIT

archive_paths=()
checksum_targets=()
signature_targets=()
sbom_targets=()

for rid in "${RIDS[@]}"; do
  publish_dir="$tmp_root/publish/$rid"
  stage_dir="$tmp_root/stage/veeling-$VERSION-$rid"

  echo "Publishing RID: $rid"
  dotnet publish "$PROJECT_PATH" -c Release -r "$rid" --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o "$publish_dir"

  mkdir -p "$stage_dir"
  cp -R "$publish_dir"/. "$stage_dir"/

  if [[ "$rid" == win-* ]]; then
    archive_path="$OUTPUT_DIR/veeling-$VERSION-$rid.zip"
    if command -v zip >/dev/null 2>&1; then
      (
        cd "$tmp_root/stage"
        zip -r "$archive_path" "veeling-$VERSION-$rid" >/dev/null
      )
    elif command -v powershell.exe >/dev/null 2>&1; then
      stage_dir_win="$(cygpath -w "$stage_dir")"
      archive_path_win="$(cygpath -w "$archive_path")"
      powershell.exe -NoProfile -Command "Compress-Archive -Path '$stage_dir_win\\*' -DestinationPath '$archive_path_win' -Force" >/dev/null
    elif command -v pwsh >/dev/null 2>&1; then
      pwsh -NoProfile -Command "Compress-Archive -Path '$stage_dir/*' -DestinationPath '$archive_path' -Force" >/dev/null
    else
      echo "No zip-capable tool found (powershell.exe, pwsh, or zip)." >&2
      exit 1
    fi
  else
    archive_path="$OUTPUT_DIR/veeling-$VERSION-$rid.tar.gz"
    tar -C "$tmp_root/stage" -czf "$archive_path" "veeling-$VERSION-$rid"
  fi

  archive_paths+=("$archive_path")
  checksum_targets+=("$archive_path")
  echo "Created: $archive_path"
done

dotnet pack "$PROJECT_PATH" -c Release -o "$NUPKG_DIR"
nupkg_path="$NUPKG_DIR/veeling.$VERSION.nupkg"
if [[ ! -f "$nupkg_path" ]]; then
  echo "Expected package not found: $nupkg_path" >&2
  exit 1
fi

checksum_targets+=("$nupkg_path")
echo "Created/verified package: $nupkg_path"

if [[ "$SIGNING_MODE" != "off" ]]; then
  if signing_prereq_nuget_ok; then
    nuget_sign_cmd=(dotnet nuget sign "$nupkg_path" --certificate-path "$VEELING_NUGET_SIGN_CERT_PATH" --hash-algorithm SHA256)
    if [[ -n "${VEELING_NUGET_SIGN_CERT_PASSWORD:-}" ]]; then
      nuget_sign_cmd+=(--certificate-password "$VEELING_NUGET_SIGN_CERT_PASSWORD")
    fi
    if [[ -n "${VEELING_NUGET_SIGN_TIMESTAMP_URL:-}" ]]; then
      nuget_sign_cmd+=(--timestamper "$VEELING_NUGET_SIGN_TIMESTAMP_URL")
    fi

    "${nuget_sign_cmd[@]}"
    echo "Signed NuGet package: $nupkg_path"
  else
    require_or_warn "$SIGNING_MODE" "Skipping NuGet signing: missing VEELING_NUGET_SIGN_CERT_PATH or file does not exist."
  fi

  if signing_prereq_archive_ok; then
    for archive in "${archive_paths[@]}"; do
      sig_path="$archive.asc"
      gpg --batch --yes --armor --detach-sign --local-user "$VEELING_GPG_KEY_ID" --output "$sig_path" "$archive"
      signature_targets+=("$sig_path")
      echo "Signed archive: $sig_path"
    done
  else
    require_or_warn "$SIGNING_MODE" "Skipping archive signing: gpg/VEELING_GPG_KEY_ID not available."
  fi
fi

if [[ "$SBOM_MODE" != "off" ]]; then
  sbom_tool="$(detect_sbom_tool)"
  if [[ -z "$sbom_tool" ]]; then
    require_or_warn "$SBOM_MODE" "Skipping SBOM generation: syft not available."
  else
    sbom_file="$OUTPUT_DIR/sbom.$VERSION.cdx.json"
    if [[ "$sbom_tool" == "syft" ]]; then
      syft "$nupkg_path" -o cyclonedx-json > "$sbom_file"
      sbom_targets+=("$sbom_file")
      echo "Generated SBOM: $sbom_file"
    fi
  fi
fi

for sig in "${signature_targets[@]}"; do
  checksum_targets+=("$sig")
done

for sbom in "${sbom_targets[@]}"; do
  checksum_targets+=("$sbom")
done

if [[ "$PROVENANCE_MODE" != "off" ]]; then
  if provenance_prereq_ok; then
    provenance_file="$OUTPUT_DIR/provenance.intoto.json"
    commit_sha="$(git rev-parse HEAD)"
    repo_url="${VEELING_REPOSITORY_URL:-https://github.com/llaurila/veeling}"
    created_at="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"

    mapfile -t prov_targets < <(printf '%s\n' "${checksum_targets[@]}" | sort)
    subject_json=""
    for target in "${prov_targets[@]}"; do
      if command -v sha256sum >/dev/null 2>&1; then
        digest="$(sha256sum "$target" | awk '{print $1}')"
      else
        digest="$(shasum -a 256 "$target" | awk '{print $1}')"
      fi
      name="$(basename "$target")"
      subject_json+="{\"name\":\"$name\",\"digest\":{\"sha256\":\"$digest\"}},"
    done
    subject_json="[${subject_json%,}]"

    cat > "$provenance_file" <<EOF
{
  "_type": "https://in-toto.io/Statement/v1",
  "subject": $subject_json,
  "predicateType": "https://slsa.dev/provenance/v1",
  "predicate": {
    "buildDefinition": {
      "buildType": "https://veeling.dev/release-archives-script/v1",
      "externalParameters": {
        "tag": "$TAG",
        "version": "$VERSION",
        "signing_mode": "$SIGNING_MODE",
        "sbom_mode": "$SBOM_MODE"
      }
    },
    "runDetails": {
      "builder": {
        "id": "veeling/scripts/release-archives.sh"
      },
      "metadata": {
        "invocationId": "$commit_sha",
        "startedOn": "$created_at",
        "finishedOn": "$created_at"
      }
    },
    "materials": [
      {
        "uri": "$repo_url",
        "digest": {
          "sha1": "$commit_sha"
        }
      }
    ]
  }
}
EOF

    checksum_targets+=("$provenance_file")
    echo "Generated provenance attestation: $provenance_file"
  else
    require_or_warn "$PROVENANCE_MODE" "Skipping provenance generation: git metadata unavailable."
  fi
fi

: > "$checksum_file"
mapfile -t sorted_targets < <(printf '%s\n' "${checksum_targets[@]}" | sort)
for archive in "${sorted_targets[@]}"; do
  if command -v sha256sum >/dev/null 2>&1; then
    digest="$(sha256sum "$archive" | awk '{print $1}')"
  elif command -v shasum >/dev/null 2>&1; then
    digest="$(shasum -a 256 "$archive" | awk '{print $1}')"
  else
    echo "No checksum tool found (sha256sum or shasum)." >&2
    exit 1
  fi

  file_name="$(basename "$archive")"
  printf '%s  %s\n' "$digest" "$file_name" >> "$checksum_file"
done

echo "Created: $checksum_file"
