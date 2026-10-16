#!/usr/bin/env bash
# End-to-end: real PureSharp nupkg, RT0001, suppression and analyzer removal.
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p "$work/packages" "$work/consumer"

dotnet restore "$repo_root/PureSharp.slnx"
dotnet build "$repo_root/PureSharp.slnx" --configuration Release --no-restore
dotnet pack "$repo_root/src/PureSharp.Core/PureSharp.Core.csproj" \
  --configuration Release --no-build --output "$work/packages"

shopt -s nullglob
packages=("$work/packages"/loach.PureSharp.*.nupkg)
shopt -u nullglob
[[ "${#packages[@]}" -eq 1 ]] || { echo "Exactly one PureSharp package required" >&2; exit 2; }
name="$(basename "${packages[0]}")"
version="${name#loach.PureSharp.}"
version="${version%.nupkg}"

cat >"$work/consumer/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="loach.PureSharp" Version="$version" />
  </ItemGroup>
</Project>
EOF

cat >"$work/consumer/NuGet.Config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources><clear />
<add key="local" value="$work/packages" />
<add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
</packageSources></configuration>
EOF

cat >"$work/consumer/Program.cs" <<'EOF'
using PureSharp.Core;
internal static class Program
{
    private static int Counter = 1;
    [PureMethod]
    private static int ReadCounter() => Counter;
    private static void Main() => System.Console.WriteLine(ReadCounter());
}
EOF

dotnet restore "$work/consumer/Consumer.csproj" --configfile "$work/consumer/NuGet.Config"
run_feedback() {
  python "$repo_root/scripts/agent_diagnostic_feedback.py" \
    --root "$work/consumer" --project Consumer.csproj --backend build --strict \
    >"$work/result.json"
}

set +e
run_feedback
first=$?
set -e
[[ $first -eq 1 ]] || { cat "$work/result.json"; echo "RT0001 must fail" >&2; exit 1; }
python - "$work/result.json" <<'PY'
import json,sys
x=json.load(open(sys.argv[1]))
assert x["status"] == "DENY" and any(d["id"] == "RT0001" for d in x["diagnostics"]), x
PY

cat >"$work/consumer/.editorconfig" <<'EOF'
root = true
[*.cs]
dotnet_diagnostic.RT0001.severity = none
EOF
set +e
run_feedback
suppressed=$?
set -e
[[ $suppressed -eq 1 ]] || { cat "$work/result.json"; echo "Suppression must fail strict gate" >&2; exit 1; }
python - "$work/result.json" <<'PY'
import json,sys
x=json.load(open(sys.argv[1]))
assert x["status"] == "DENY" and any(f["kind"] == "severity-override" for f in x["auditFindings"]), x
PY
rm "$work/consumer/.editorconfig"

cat >"$work/consumer/Program.cs" <<'EOF'
using PureSharp.Core;
internal static class Program
{
    [PureMethod]
    private static int ReadCounter() => 1;
    private static void Main() => System.Console.WriteLine(ReadCounter());
}
EOF
run_feedback
python - "$work/result.json" <<'PY'
import json,sys
x=json.load(open(sys.argv[1]))
assert x["status"] == "ALLOW", x
PY

# If the analyzer package is removed, a green validation is forbidden.
python - "$work/consumer/Consumer.csproj" <<'PY'
import pathlib,re,sys
p=pathlib.Path(sys.argv[1])
s,n=re.subn(r'<PackageReference\s+Include="loach\.PureSharp"[^>]*/>', "", p.read_text(), count=1)
assert n == 1
p.write_text(s)
PY
set +e
run_feedback
removed=$?
set -e
[[ $removed -eq 2 ]] || { cat "$work/result.json"; echo "Missing analyzer must fail closed" >&2; exit 1; }
python - "$work/result.json" <<'PY'
import json,sys
x=json.load(open(sys.argv[1]))
assert x["status"] == "ERROR" and "reference not present" in x["error"], x
PY
echo "PureSharp: real consumer red, suppression deny, fixed green, and removed analyzer denial verified."
