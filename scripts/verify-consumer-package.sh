#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_dir="${1:-$repo_root/out}"

shopt -s nullglob
packages=("$package_dir"/loach.PureSharp.*.nupkg)
shopt -u nullglob

if (( ${#packages[@]} != 1 )); then
  echo "Expected exactly one loach.PureSharp .nupkg in $package_dir, found ${#packages[@]}." >&2
  exit 1
fi

package="${packages[0]}"
filename="$(basename "$package")"
version="${filename#loach.PureSharp.}"
version="${version%.nupkg}"

contents="$(unzip -Z1 "$package")"
for required in \
  "README.md" \
  "lib/netstandard2.0/PureSharp.Core.dll" \
  "analyzers/dotnet/cs/PureSharp.Core.dll"
do
  if ! grep -Fxq "$required" <<<"$contents"; then
    echo "Package is missing required entry: $required" >&2
    exit 1
  fi
done

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

cat >"$work/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="loach.PureSharp" Version="$version" />
  </ItemGroup>
</Project>
EOF

cat >"$work/NuGet.Config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$package_dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

cat >"$work/Program.cs" <<'EOF'
using PureSharp.Core;

internal static class Program
{
    private static int Counter;

    [PureMethod]
    private static int ReadCounter() => Counter;

    private static void Main() => System.Console.WriteLine(ReadCounter());
}
EOF

pushd "$work" >/dev/null

dotnet restore --configfile NuGet.Config

set +e
dotnet build --no-restore >first-build.log 2>&1
first_status=$?
set -e

if (( first_status == 0 )); then
  cat first-build.log >&2
  echo "Expected consumer build to fail with RT0001, but it succeeded." >&2
  exit 1
fi

if ! grep -q "RT0001" first-build.log; then
  cat first-build.log >&2
  echo "Consumer build failed, but RT0001 was not reported." >&2
  exit 1
fi

cat >.editorconfig <<'EOF'
root = true

[*.cs]
dotnet_diagnostic.RT0001.severity = none
EOF

dotnet build --no-restore

popd >/dev/null

echo "Consumer package verification passed for loach.PureSharp $version."
