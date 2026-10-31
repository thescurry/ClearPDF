#!/usr/bin/env bash
# Fail unless the bundled executable and PDFium are Intel x86_64 only.
set -euo pipefail

APP="${1:?ClearPDF.app path}"
BIN="$APP/Contents/MacOS/ClearPDF"
DYLIB="$APP/Contents/MacOS/pdfium.dylib"

for f in "$BIN" "$DYLIB"; do
  if [[ ! -f "$f" ]]; then
    echo "error: missing $f" >&2
    exit 1
  fi
  echo "== file $f"
  file "$f"
  echo "== lipo $f"
  lipo -info "$f"
  info="$(lipo -info "$f")"
  if ! grep -Eq 'architecture: x86_64$|are: x86_64$' <<<"$info"; then
    echo "error: expected a thin x86_64 binary: $info" >&2
    exit 1
  fi
  if grep -Eq 'arm64|x86_64h' <<<"$info"; then
    echo "error: unexpected extra arch: $info" >&2
    exit 1
  fi
done

if find "$APP" -name '*.pdb' | grep -q .; then
  echo "error: bundle still contains .pdb files" >&2
  find "$APP" -name '*.pdb'
  exit 1
fi

if [[ -e "$APP/Contents/MacOS/runtimes/osx-x64/native/pdfium.dylib" \
   || -e "$APP/Contents/MacOS/runtimes/osx-x64/native/libpdfium.dylib" ]]; then
  echo "error: duplicate pdfium still under runtimes/" >&2
  exit 1
fi

echo "x64 bundle checks passed"
