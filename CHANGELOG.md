# Changelog

## 1.3.0

Scan to PDF on Windows.

- **File → Scan…** (empty window) and a toolbar Scan button (reader) open a scan dialog: pick scanner, source (flatbed / feeder), color mode, DPI (150 / 200 / 300), optional duplex when the device reports it.
- Pages land in a thumbnail strip: reorder (up/down or drag), rotate 90°, delete, **Scan more**.
- **Save as new PDF** or **Append to open PDF**. Append never writes the open file in place — it builds a temp file, then replaces.
- Core (`ClearPDF.Core`) is still platform-neutral: `IScanner` / `IScannerProvider`, `ScanSession`, mock scanner, and a PDFsharp 6.2.4 builder (JPEG / DCTDecode for color and grayscale). Page size is pixels × 72 / DPI.
- Windows talks to scanners through **WIA 2.0** COM (`wiaaut.dll`), not WinRT `Windows.Devices.Scanners`.
- Version surfaces: csproj, AssemblyInfo, Inno `AppVersion`, About, this changelog.

## 1.2.0

Page-selection pack: multi-select thumbs, Save pages as…, Print selected, Save As choice card.
