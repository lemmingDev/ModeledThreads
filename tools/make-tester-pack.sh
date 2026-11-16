#!/bin/sh
# Builds the folder and zip handed to someone testing Inventor 2025 or 2026.
#
#   ./tools/make-tester-pack.sh [output zip]
#
# It stages: the installable bundle, the harness and a runner that writes one
# report file, and a plain-text guide. The harness runs against the very DLL
# that gets installed, so a pass means the shipped bits passed.

set -eu

root=$(cd "$(dirname "$0")/.." && pwd)
out=${1:-"$HOME/Downloads/ModeledThreads-Tester-2025-2026.zip"}
stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT

pack="$stage/ModeledThreads-Tester"
mkdir -p "$pack/test" "$pack/report"

cp -R "$root/bundle" "$pack/"
cp "$root/install.bat" "$root/install.ps1" "$pack/"
cp "$root/tools/tester/READ-THIS-FIRST.txt" "$pack/"
cp "$root/tools/tester/run-tests.bat" "$root/tools/tester/run-tests.ps1" "$pack/test/"

harness="$root/tools/TestHarness/bin/Release"
cp "$harness/ThreadHarness.exe" "$harness/ThreadHarness.exe.config" \
   "$harness/Autodesk.Inventor.Interop.dll" "$pack/test/"

# Test exactly the assembly that install.bat puts on the machine.
cp "$root/bundle/ModeledThreads.bundle/Contents/ModeledThreads.dll" "$pack/test/ModeledThreads.dll"

rm -f "$out"
(cd "$stage" && zip -qr "$out" ModeledThreads-Tester -x '*.DS_Store')

echo "Wrote $out"
echo
unzip -l "$out" | tail -20
echo
echo "Bundle DLL:   $(shasum -a 256 "$pack/bundle/ModeledThreads.bundle/Contents/ModeledThreads.dll" | cut -d' ' -f1)"
echo "Packaged DLL: $(shasum -a 256 "$pack/test/ModeledThreads.dll" | cut -d' ' -f1)"
echo "Zip:          $(shasum -a 256 "$out" | cut -d' ' -f1)"
