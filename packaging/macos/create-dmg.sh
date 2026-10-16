#!/usr/bin/env bash
# Wrap ClearPDF.app in a UDZO .dmg. Requires macOS hdiutil (CI: macos-13).
set -euo pipefail

APP="${1:?path to ClearPDF.app}"
OUT="${2:?output .dmg path}"

if [[ ! -d "$APP" ]]; then
  echo "error: app bundle missing: $APP" >&2
  exit 1
fi

mkdir -p "$(dirname "$OUT")"

if ! command -v hdiutil >/dev/null 2>&1; then
  echo "error: hdiutil not found (this script is for macOS Intel CI)" >&2
  exit 1
fi

# Stage a folder so the DMG volume is named ClearPDF and contains only the app.
STAGE="$(mktemp -d)/ClearPDF"
mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/ClearPDF.app"

hdiutil create -volname "ClearPDF" -srcfolder "$STAGE" -ov -format UDZO "$OUT"
echo "$OUT"
