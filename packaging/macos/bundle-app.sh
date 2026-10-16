#!/usr/bin/env bash
# Build ClearPDF.app from a published osx-x64 folder.
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

ICON_SRC="$ROOT/assets/app-icon.png"
if command -v sips >/dev/null 2>&1 && command -v iconutil >/dev/null 2>&1 && [[ -f "$ICON_SRC" ]]; then
  ICONSET="$(mktemp -d)/ClearPDF.iconset"
  mkdir -p "$ICONSET"
  for sz in 16 32 128 256 512; do
    # macos-15 sips warns and writes JPEG unless format is forced to png.
    sips -s format png -z "$sz" "$sz" "$ICON_SRC" --out "$ICONSET/icon_${sz}x${sz}.png" >/dev/null
    sips -s format png -z $((sz * 2)) $((sz * 2)) "$ICON_SRC" --out "$ICONSET/icon_${sz}x${sz}@2x.png" >/dev/null
  done
  if ! iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/ClearPDF.icns"; then
    echo "iconutil failed — app will use the embedded PNG icon"
  fi
else
  echo "sips/iconutil not available — app will use the embedded PNG icon"
fi

"$ROOT/packaging/macos/ensure-pdfium.sh" "$APP/Contents/MacOS"

if [[ -f "$APP/Contents/MacOS/ClearPDF" ]]; then
  chmod +x "$APP/Contents/MacOS/ClearPDF"
else
  echo "error: published executable ClearPDF not found in $PUBLISH" >&2
  exit 1
fi

echo "$APP"
