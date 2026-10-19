#!/bin/zsh
# Type-checks the PX assemblies (no Unity editor needed; used when the editor is unavailable) with Unity's own Roslyn, outside the editor, using the references and defines from
# the project files Unity generated. Usage: Tools/typecheck.sh [runtime|editor|tests|all]
set -u
PROJECT="$(cd "$(dirname "$0")/.." && pwd)/PX"
UNITY=/Applications/Unity/Hub/Editor/6000.3.11f1/Unity.app/Contents
DOTNET=$UNITY/Resources/Scripting/NetCoreRuntime/dotnet
CSC=$UNITY/Resources/Scripting/DotNetSdkRoslyn/csc.dll
OUT="${PX_TYPECHECK_OUT:-/tmp/px-typecheck}"
mkdir -p "$OUT"
cd "$PROJECT"

refs_of() {
  # Reference HintPaths of a csproj, absolute, one per line, minus the PX assemblies themselves.
  grep -o "<HintPath>[^<]*" "$1" | sed 's/<HintPath>//' | grep -v "ScriptAssemblies/PX\." | while read -r r; do
    case "$r" in /*) echo "$r";; *) echo "$PROJECT/$r";; esac
  done
}
defines_of() { grep -m1 -o "<DefineConstants>[^<]*" "$1" | sed 's/<DefineConstants>//'; }

compile() {
  local name=$1 csproj=$2 srcdir=$3; shift 3
  local extra=("$@")
  local args=("-noconfig" "-nostdlib" "-target:library" "-langversion:9.0" "-nowarn:0169,USG0001,1701,1702" "-warn:0"
    "-define:$(defines_of "$csproj")" "-out:$OUT/$name.dll")
  while read -r r; do args+=("-r:$r"); done < <(refs_of "$csproj")
  for e in "${extra[@]}"; do args+=("-r:$e"); done
  local files=("${(@f)$(find "$srcdir" -name '*.cs')}")
  "$DOTNET" "$CSC" "${args[@]}" "${files[@]}" 2>&1 | grep -v "^$" | grep -v "warning CS" | sed "s|$PROJECT/||"
  return ${pipestatus[1]}
}

what=${1:-all}
result=0
if [[ $what == runtime || $what == all ]]; then
  echo "== PX.Runtime"; compile PX.Runtime PX.Runtime.csproj Assets/PX/Scripts/Runtime || result=1
fi
if [[ $what == editor || $what == all ]]; then
  echo "== PX.Editor"; compile PX.Editor PX.Editor.csproj Assets/PX/Scripts/Editor "$OUT/PX.Runtime.dll" || result=1
fi
if [[ $what == tests || $what == all ]]; then
  echo "== PX.Tests.PlayMode"; compile PX.Tests.PlayMode PX.Tests.PlayMode.csproj Assets/PX/Tests/PlayMode "$OUT/PX.Runtime.dll" || result=1
  echo "== PX.Tests.EditMode"; compile PX.Tests.EditMode PX.Tests.EditMode.csproj Assets/PX/Tests/EditMode "$OUT/PX.Runtime.dll" "$OUT/PX.Editor.dll" || result=1
fi
exit $result
