#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if [[ -n "$(git status --porcelain --untracked-files=all)" ]]; then
  echo "Release verification must start from a clean working tree." >&2
  git status --short >&2
  exit 1
fi

expected_version="${1:-}"
version="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' Directory.Build.props | head -n1)"

if [[ -z "$version" ]]; then
  echo "Could not read <Version> from Directory.Build.props." >&2
  exit 1
fi

if [[ -n "$expected_version" && "$version" != "$expected_version" ]]; then
  echo "Version mismatch: expected $expected_version but SSOT is $version." >&2
  exit 1
fi

mapfile -t version_lines < <(git grep -n '<Version>' -- '*.props' '*.csproj' || true)
if (( ${#version_lines[@]} != 1 )) || [[ "${version_lines[0]}" != Directory.Build.props:* ]]; then
  printf 'Expected exactly one repository <Version> definition in Directory.Build.props. Found:\n%s\n' "${version_lines[*]:-<none>}" >&2
  exit 1
fi

for required in \
  README.md \
  README_ja.md \
  CHANGELOG.md \
  docs/DIAGNOSTICS.md \
  docs/GETTING_STARTED.md \
  docs/MIGRATION-1.0.md \
  docs/COMPATIBILITY.md \
  docs/RELEASE.md
 do
  if [[ ! -f "$required" ]]; then
    echo "Missing release document: $required" >&2
    exit 1
  fi
 done

rm -rf out

dotnet restore
dotnet build --configuration Release --no-restore -warnaserror:RS2007
dotnet test --configuration Release --no-build --verbosity normal
dotnet pack src/PureSharp.Core/PureSharp.Core.csproj --configuration Release -o out --no-build

package="out/loach.PureSharp.${version}.nupkg"
if [[ ! -f "$package" ]]; then
  echo "Expected package $package was not produced." >&2
  exit 1
fi

nuspec_version="$(unzip -p "$package" '*.nuspec' | sed -n 's:.*<version>\([^<]*\)</version>.*:\1:p' | head -n1)"
if [[ "$nuspec_version" != "$version" ]]; then
  echo "Package nuspec version mismatch: expected $version, found $nuspec_version." >&2
  exit 1
fi

bash scripts/verify-consumer-package.sh out
bash scripts/verify-analyzer-performance.sh out

if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Tracked working tree changed during release verification." >&2
  git status --short >&2
  exit 1
fi

echo "Release verification passed for PureSharp $version ($package)."
