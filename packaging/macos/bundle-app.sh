#!/usr/bin/env bash
# Assemble unsigned ClearPDF.app from a published osx-x64 folder.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PUBLISH="${1:-$ROOT/publish/osx-x64}"
DIST="${2:-$ROOT/dist/macos}"
APP="$DIST/ClearPDF.app"

if [[ ! -d "$PUBLISH" ]]; then
  echo "error: publish directory missing: $PUBLISH" >&2
  exit 1
fi

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp -a "$PUBLISH"/. "$APP/Contents/MacOS/"
cp "$ROOT/packaging/macos/Info.plist" "$APP/Contents/Info.plist"
printf 'APPL????' > "$APP/Contents/PkgInfo"

if [[ -f "$ROOT/packaging/macos/ClearPDF.icns" ]]; then
  cp "$ROOT/packaging/macos/ClearPDF.icns" "$APP/Contents/Resources/ClearPDF.icns"
else
  echo "warning: packaging/macos/ClearPDF.icns missing — app will use the embedded PNG" >&2
fi

"$ROOT/packaging/macos/ensure-pdfium.sh" "$APP/Contents/MacOS"

if [[ -f "$APP/Contents/MacOS/ClearPDF" ]]; then
  chmod +x "$APP/Contents/MacOS/ClearPDF"
else
  echo "error: published executable ClearPDF not found in $PUBLISH" >&2
  exit 1
fi

echo "$APP"
