#!/usr/bin/env bash
# Publish osx-x64, assemble unsigned ClearPDF.app, zip it.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PUBLISH="${PUBLISH_DIR:-$ROOT/publish/osx-x64}"
DIST="${DIST_DIR:-$ROOT/dist/macos}"
ZIP="$DIST/ClearPDF-macos-x64.zip"

export DOTNET_CLI_TELEMETRY_OPTOUT=1

dotnet publish "$ROOT/src/ClearPDF.Mac/ClearPDF.Mac.csproj" \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishReadyToRun=false \
  -o "$PUBLISH"

"$ROOT/packaging/macos/bundle-app.sh" "$PUBLISH" "$DIST"

rm -f "$ZIP"
(
  cd "$DIST"
  zip -ry "ClearPDF-macos-x64.zip" ClearPDF.app
)

echo "$ZIP"
