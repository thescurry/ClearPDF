#!/usr/bin/env bash
# Copy Docnet's osx-x64 PDFium dylib next to the ClearPDF executable.
# Does not download natives — ABI must match Docnet.Core 2.6.0.
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

# One file on disk; the other name is a symlink (Docnet DllImport("pdfium")).
if [[ "$base" == "pdfium.dylib" ]]; then
  rm -f "$DEST/libpdfium.dylib"
  ln -s pdfium.dylib "$DEST/libpdfium.dylib"
fi
if [[ "$base" == "libpdfium.dylib" && ! -f "$DEST/pdfium.dylib" ]]; then
  cp -f "$DEST/libpdfium.dylib" "$DEST/pdfium.dylib"
  rm -f "$DEST/libpdfium.dylib"
  ln -s pdfium.dylib "$DEST/libpdfium.dylib"
fi

# Drop the RID-probed copy so the bundle is not carrying pdfium twice.
rm -f "$DEST/runtimes/osx-x64/native/pdfium.dylib" \
      "$DEST/runtimes/osx-x64/native/libpdfium.dylib"
rmdir "$DEST/runtimes/osx-x64/native" 2>/dev/null || true
rmdir "$DEST/runtimes/osx-x64" 2>/dev/null || true
rmdir "$DEST/runtimes/osx/native" 2>/dev/null || true
rmdir "$DEST/runtimes/osx" 2>/dev/null || true
