#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
unity_dir="${UNITY_EDITOR_DIR:-/home/Grace/Unity/Hub/Editor/2022.3.62f3/Editor}"
mono_bin="$unity_dir/Data/MonoBleedingEdge/bin/mono"
compiler="$unity_dir/Data/MonoBleedingEdge/lib/mono/4.5/mcs.exe"
test_dir=$(mktemp -d /tmp/vwater-visual.XXXXXX)
trap 'rm -rf "$test_dir"' EXIT
sources=(tests/vwater-skim/VisualTests.cs Assets/GraceVWaterOriginalTest/Runtime/DownwashMath.cs)
if test -f Assets/GraceVWaterOriginalTest/Runtime/DownwashVisualMath.cs; then
    sources+=(Assets/GraceVWaterOriginalTest/Runtime/DownwashVisualMath.cs)
fi
if test -f Assets/GraceVWaterOriginalTest/Editor/DownwashCurveDraft.cs; then
    sources+=(Assets/GraceVWaterOriginalTest/Editor/DownwashCurveDraft.cs)
fi
"$mono_bin" "$compiler" -langversion:latest -out:"$test_dir/VisualTests.exe" "${sources[@]}"
"$mono_bin" "$test_dir/VisualTests.exe"
