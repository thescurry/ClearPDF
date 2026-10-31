# Changelog

## 1.3.0

Scan to PDF on Windows.

- **File → Scan…** (empty window, **Ctrl+Shift+S**) and a toolbar Scan button (reader) open a scan dialog. Last-used scanner and settings are remembered.
- One-click **Scan** defaults: 300 DPI, color, auto page size. Feeder + duplex when the device reports them. Flatbed is a one-page-then-add-more loop.
- Pages appear in the strip as they arrive. Per page: rotate, drag-reorder, delete, rescan, crop, trim margins. Auto-straighten and drop-blank-pages are on by default (toggles).
- **Save as new PDF** or **Add to open PDF** at the start, after a chosen page, or at the end. Writes use a temp file, then replace.
- Core (`ClearPDF.Core`) is UI-free: `IScanner`, `ScanViewModel`, page pipeline, mock scanner, PDFsharp 6.2.4 builder (JPEG / DCTDecode). WPF is a thin dialog + MainWindow hook.
- Windows talks to scanners through **WIA 2.0** COM (`wiaaut.dll`) on a background STA thread. TWAIN is not bundled (see README). OCR is a clean `IScanOcr` hook only — no engine ships.
- Version surfaces: csproj, AssemblyInfo, Inno `AppVersion`, About, this changelog.

## 1.2.0

Page-selection pack: multi-select thumbs, Save pages as…, Print selected, Save As choice card.
