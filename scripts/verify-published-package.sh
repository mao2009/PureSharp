#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: verify-published-package.sh <version>}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

cat >"$work/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="loach.PureSharp" Version="$version" />
  </ItemGroup>
</Project>
EOF

cat >"$work/NuGet.Config" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

cat >"$work/Probe.cs" <<'EOF'
using PureSharp.Core;

public static class Probe
{
    private static int Counter = 1;

    [PureMethod]
    public static int ReadCounter() => Counter;
}
EOF

export NUGET_PACKAGES="$work/packages"
pushd "$work" >/dev/null

max_attempts=30
retry_delay_seconds=20
restored=false
for attempt in $(seq 1 "$max_attempts"); do
  if dotnet restore --configfile NuGet.Config --no-cache --force; then
    restored=true
    break
  fi

  echo "Published package $version is not available from nuget.org yet (attempt $attempt/$max_attempts)." >&2
  if (( attempt < max_attempts )); then
    sleep "$retry_delay_seconds"
  fi
done

if [[ "$restored" != true ]]; then
  echo "Could not restore loach.PureSharp $version from nuget.org after $max_attempts attempts." >&2
  exit 1
fi

set +e
dotnet build --no-restore >first-build.log 2>&1
first_status=$?
set -e

if (( first_status == 0 )); then
  cat first-build.log >&2
  echo "Expected published consumer build to fail with RT0001, but it succeeded." >&2
  exit 1
fi

if ! grep -q 'RT0001' first-build.log; then
  cat first-build.log >&2
  echo "Published consumer build failed, but RT0001 was not reported." >&2
  exit 1
fi

cat >.editorconfig <<'EOF'
root = true

[*.cs]
dotnet_diagnostic.RT0001.severity = none
EOF

dotnet build --no-restore

popd >/dev/null

echo "Published NuGet consumer verification passed for loach.PureSharp $version."
