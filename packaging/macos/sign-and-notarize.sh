#!/usr/bin/env bash
# Optional Developer ID sign + notarize.
# NEVER prompts. NEVER opens Apple ID / browser sign-in / notarytool store-credentials.
# Missing secrets → exit 0 so CI still ships an unsigned .dmg.
set -euo pipefail

APP="${1:?path to ClearPDF.app}"

need=(
  APPLE_CERTIFICATE_BASE64
  APPLE_CERTIFICATE_PASSWORD
  APPLE_SIGNING_IDENTITY
)

missing=0
for v in "${need[@]}"; do
  if [[ -z "${!v:-}" ]]; then
    missing=1
    break
  fi
done

if [[ "$missing" -eq 1 ]]; then
  echo "Signing secrets not present — shipping unsigned ClearPDF.app"
  exit 0
fi

if [[ ! -d "$APP" ]]; then
  echo "error: app bundle missing: $APP" >&2
  exit 1
fi

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
ENTITLEMENTS="$ROOT/packaging/macos/entitlements.plist"
KEYCHAIN="clearpdf-signing.keychain-db"
KEYCHAIN_PW="$(openssl rand -hex 16)"
P12="$(mktemp).p12"

cleanup() {
  rm -f "$P12"
  security delete-keychain "$KEYCHAIN" >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "$APPLE_CERTIFICATE_BASE64" | base64 --decode > "$P12"

security create-keychain -p "$KEYCHAIN_PW" "$KEYCHAIN"
security set-keychain-settings -lut 21600 "$KEYCHAIN"
security unlock-keychain -p "$KEYCHAIN_PW" "$KEYCHAIN"
security import "$P12" -k "$KEYCHAIN" -P "$APPLE_CERTIFICATE_PASSWORD" -T /usr/bin/codesign -T /usr/bin/security
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$KEYCHAIN_PW" "$KEYCHAIN" >/dev/null
security list-keychain -d user -s "$KEYCHAIN"

codesign --force --deep --options runtime \
  --entitlements "$ENTITLEMENTS" \
  --sign "$APPLE_SIGNING_IDENTITY" \
  "$APP"

echo "Signed $APP"

if [[ -z "${APPLE_NOTARY_APPLE_ID:-}" || -z "${APPLE_NOTARY_TEAM_ID:-}" || -z "${APPLE_NOTARY_APP_PASSWORD:-}" ]]; then
  echo "Notarization secrets not present — signed but not notarized"
  exit 0
fi

ZIP="$(mktemp).zip"
ditto -c -k --keepParent "$APP" "$ZIP"

# App-specific password from secrets only. No store-credentials, no interactive login.
xcrun notarytool submit "$ZIP" \
  --apple-id "$APPLE_NOTARY_APPLE_ID" \
  --team-id "$APPLE_NOTARY_TEAM_ID" \
  --password "$APPLE_NOTARY_APP_PASSWORD" \
  --wait

xcrun stapler staple "$APP"
rm -f "$ZIP"
echo "Notarized and stapled $APP"
