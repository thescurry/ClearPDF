#!/usr/bin/env bash
# Copy Docnet's osx-x64 PDFium dylib next to the ClearPDF executable.
# Does not download random natives — ABI must match Docnet.Core 2.6.0.
set -euo pipefail

DEST="${1:?destination directory}"
mkdir -p "$DEST"

found=""
for candidate in \
  "$DEST/pdfium.dylib" \
  "$DEST/libpdfium.dylib" \
  "$DEST/runtimes/osx-x64/native/pdfium.dylib" \
  "$DEST/runtimes/osx-x64/native/libpdfium.dylib" \
  "$DEST/runtimes/osx/native/pdfium.dylib" \
  "$DEST/runtimes/osx/native/libpdfium.dylib"
do
  if [[ -f "$candidate" ]]; then
    found="$candidate"
    break
  fi
done

if [[ -z "$found" ]]; then
  echo "error: pdfium.dylib not found under $DEST (Docnet.Core osx-x64 native missing)" >&2
  echo "publish with: dotnet publish -r osx-x64 --self-contained true" >&2
  exit 1
fi

base="$(basename "$found")"
if [[ "$(cd "$(dirname "$found")" && pwd)" != "$(cd "$DEST" && pwd)" ]]; then
  cp -f "$found" "$DEST/$base"
  echo "copied $found -> $DEST/$base"
else
  echo "pdfium already beside exe: $found"
fi

# Docnet DllImport("pdfium") resolves libpdfium.dylib OR pdfium.dylib.
if [[ "$base" == "pdfium.dylib" && ! -f "$DEST/libpdfium.dylib" ]]; then
  cp -f "$DEST/pdfium.dylib" "$DEST/libpdfium.dylib"
fi
if [[ "$base" == "libpdfium.dylib" && ! -f "$DEST/pdfium.dylib" ]]; then
  cp -f "$DEST/libpdfium.dylib" "$DEST/pdfium.dylib"
fi
