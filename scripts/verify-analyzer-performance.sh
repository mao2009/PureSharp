#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_dir="${1:-$repo_root/out}"
package_dir="$(cd "$package_dir" && pwd)"
max_ms="${PURESHARP_ANALYZER_PERF_MAX_MS:-15000}"
source_count="${PURESHARP_ANALYZER_PERF_SOURCE_COUNT:-250}"

if ! [[ "$max_ms" =~ ^[0-9]+$ ]] || (( max_ms <= 0 )); then
  echo "PURESHARP_ANALYZER_PERF_MAX_MS must be a positive integer." >&2
  exit 2
fi

if ! [[ "$source_count" =~ ^[0-9]+$ ]] || (( source_count <= 0 )); then
  echo "PURESHARP_ANALYZER_PERF_SOURCE_COUNT must be a positive integer." >&2
  exit 2
fi

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

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p "$work/Sources"

cat >"$work/PerfConsumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
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
internal static class Program
{
    private static void Main() { }
}
EOF

for ((i = 1; i <= source_count; i++)); do
  printf -v suffix "%04d" "$i"
  cat >"$work/Sources/Sample${suffix}.cs" <<EOF
using PureSharp.Core;

internal static class Sample${suffix}
{
    [PureMethod]
    internal static int Run(int input)
    {
        int _first = input + ${i};
        int _second = _first * 2;
        int _third = _second - ${i};
        return _third;
    }
}
EOF
done

pushd "$work" >/dev/null

dotnet restore --configfile NuGet.Config >/dev/null

# Warm the SDK/compiler/analyzer host once so the gate primarily tracks compilation +
# analyzer execution rather than first-start package/JIT costs.
dotnet build --configuration Release --no-restore --verbosity quiet >/dev/null

start_ns="$(date +%s%N)"
dotnet build --configuration Release --no-restore --no-incremental --verbosity quiet >/dev/null
end_ns="$(date +%s%N)"
elapsed_ms=$(( (end_ns - start_ns) / 1000000 ))

popd >/dev/null

echo "PureSharp analyzer performance sample: package=$version sources=$source_count elapsed_ms=$elapsed_ms threshold_ms=$max_ms"

if (( elapsed_ms > max_ms )); then
  echo "Analyzer performance gate failed: ${elapsed_ms}ms exceeds ${max_ms}ms." >&2
  exit 1
fi
