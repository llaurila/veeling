#!/usr/bin/env bash

set -euo pipefail

TAG=""
PROJECT_PATH="Veeling.CLI/Veeling.CLI.csproj"

usage() {
  cat <<'EOF'
Usage: scripts/validate-release-tag.sh --tag <vX.Y.Z> [--project <path>]

Validate that a release tag is SemVer-compliant, annotated, and aligned with
the canonical MSBuild version.

Checks:
  1) Tag name matches ^vMAJOR.MINOR.PATCH$.
  2) Tag exists locally.
  3) Tag is annotated (not lightweight).
  4) Tag points to current HEAD.
  5) Tag version matches MSBuild Version from Directory.Build.props.

Exit codes:
  0  validation passed
  1  validation failed
  2  usage error
EOF
}

fail() {
  echo "[release-tag-check] ERROR: $1" >&2
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag)
      TAG="${2:-}"
      shift 2
      ;;
    --project)
      PROJECT_PATH="${2:-}"
      shift 2
      ;;
    --help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$TAG" ]]; then
  echo "Missing required --tag argument." >&2
  usage >&2
  exit 2
fi

if [[ ! "$TAG" =~ ^v([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
  fail "Tag '$TAG' is not SemVer-compliant. Expected format: vX.Y.Z"
fi

if ! git rev-parse -q --verify "refs/tags/$TAG" >/dev/null; then
  fail "Tag '$TAG' does not exist locally. Fetch tags or create it first."
fi

object_type="$(git for-each-ref "refs/tags/$TAG" --format='%(objecttype)')"
if [[ "$object_type" != "tag" ]]; then
  fail "Tag '$TAG' is lightweight. Use annotated tags: git tag -a $TAG -m 'Release $TAG'"
fi

tag_commit="$(git rev-list -n 1 "$TAG")"
head_commit="$(git rev-parse HEAD)"
if [[ "$tag_commit" != "$head_commit" ]]; then
  fail "Tag '$TAG' points to $tag_commit, but HEAD is $head_commit. Tag the release commit."
fi

resolved_version="$(dotnet msbuild "$PROJECT_PATH" -nologo -getProperty:Version)"
tag_version="${TAG#v}"
if [[ "$resolved_version" != "$tag_version" ]]; then
  fail "Tag version '$tag_version' does not match canonical version '$resolved_version'."
fi

echo "[release-tag-check] OK: $TAG is annotated, SemVer-compliant, points to HEAD, and matches version $resolved_version"
