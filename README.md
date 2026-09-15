# ClearPDF

Simple local PDF reader for **Windows 10+**. WPF · .NET 8 · no ribbons, AI, or cloud.

UI mocks (locked look): [`docs/mocks/`](docs/mocks/) — empty/open, reader, password modal.

## Download (Windows)

Grab the latest installer from **[Releases](https://github.com/thescurry/ClearPDF/releases)**:

1. Download `ClearPDF-Setup.exe`
2. Run it (SmartScreen may show **Unknown publisher** once — More info → Run anyway; the build is unsigned)
3. Optional: check “Set ClearPDF as default for .pdf”

Self-contained ~166 MB on disk after install. No .NET install required.


## Engine choice

**Docnet.Core** (NuGet) — managed PDFium bindings. Chosen over PdfiumViewer because it targets modern .NET, restores cleanly with `net8.0-windows`, and keeps the app on a thin WPF shell instead of WinForms hosts.

| Piece | License |
|-------|---------|
| ClearPDF app code | MIT (see [LICENSE](LICENSE)) |
| Docnet.Core | Apache-2.0 |
| PDFium (native) | Apache-2.0 / BSD-style (Google PDFium) |

Passwords are used for **view unlock only** and are never stored.

## Requirements (Win10 build machine)

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (includes Windows Desktop / WPF)
- Optional: Visual Studio 2022 with “.NET desktop development”

> This repo was scaffolded on a Linux box. **WPF does not run there.** Build and run on Windows.

## Build & run

```bat
cd ClearPDF
dotnet restore
dotnet build
dotnet run --project src\ClearPDF\ClearPDF.csproj
```

Tests (Core helpers — runs on Linux / any OS with .NET 8 SDK):

```bash
dotnet test
# equivalent: dotnet test tests/ClearPDF.Tests/ClearPDF.Tests.csproj
```

On non-Windows, the WPF project is a no-op stub so solution-level `dotnet test` only exercises `ClearPDF.Core`.

## Open from Explorer / command line

ClearPDF opens the first existing `.pdf` argument after the exe (spaces/quoted paths OK). No-arg launch still shows the empty Open screen.

```bat
ClearPDF.exe "C:\path\to\file.pdf"
```

**Code QA smoke:** associate or “Open with” ClearPDF → double-click a PDF → document renders (not the empty Open screen). Drag-drop a PDF onto the window also works. Helper: `CommandLinePdfArgs` in Core (covered by `CommandLinePdfArgsTests`).

## PDFium native notes

Docnet.Core needs **`pdfium.dll`** (and related natives) at runtime on Windows.

1. `dotnet restore` / `dotnet build` pulls Docnet.Core. Depending on package version, natives may arrive via runtime assets or a companion package.
2. If the app starts but shows the **stub page** banner (“PDFium natives not loaded”), copy `pdfium.dll` next to `ClearPDF.exe` (usually `src\ClearPDF\bin\Debug\net8.0-windows\`).
3. Official / community native builds: [bblanchon/pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) (pick the Windows x64 build matching your RID).
4. On non-Windows scaffold machines, the factory **probes** for natives and falls back to `StubPdfDocument` so the UI shell still opens. Real render/find/print quality needs Win10 + natives.

Factory entry point: `DocnetPdfDocumentFactory` in `src/ClearPDF/Services/DocnetPdfDocument.cs`.

## Logging

Local file logs only (`%LocalAppData%\ClearPDF\logs\clearpdf-yyyyMMdd.log`): open failures, password errors, unhandled exceptions — no network/Sentry/telemetry.

## Render performance

Image-heavy PDFs (brochures, full-bleed photos) used to hitch because `GetImage` ran on the UI thread and every zoom rebuilt every page via fixed `PageDimensions(2.0)` + `TransformedBitmap`.

Current architecture:

| Piece | Behavior |
|-------|----------|
| Target DPI | `RenderPage` opens a short-lived Docnet reader at `PdfRenderScale.CapPixelsPerPoint(zoom, pageW, pageH)` — no TransformedBitmap thrash |
| Caps | Longest edge ≤ 4096px, ≤ ~8M pixels/page |
| Cache | `PageRasterCache` / `PageBitmapCache` keyed by `(pageIndex, quantizedZoom)`, capped at 24 page rasters; thumbs are a separate map |
| Threads | Background raster → `Dispatcher` swaps `Image.Source`; last frame stays visible |
| Zoom | Debounced settle (120ms); pan/scroll does **not** re-Docnet or rebuild thumbs |
| Scroll | Chrome updates immediately; sharp window is debounced (80ms) and does **not** cancel in-flight neighbors |
| Virtualize | Sharp bitmaps for current ±2 pages; far pages keep low-res preview |
| Thumbnails | Sequential low-res fill at open (separate cache); scroll/zoom only update selection chrome |
| Password | `Unlock` once into memory bytes for re-raster; password never retained/stored |
| Page size | Still **PDF points** via baseline reader (`PdfPageSize`) |

Helpers: `PdfRenderScale`, `PageRenderPlanner`, `PageRasterCache<T>` in Core (unit-tested). Details: [`docs/render-perf.md`](docs/render-perf.md).

## Runnable tests without WPF

UI-free logic lives in **`src/ClearPDF.Core/`** (`net8.0`): `ZoomHelper`, `FindHelper`, `RecentFilesService`, `PdfPageSize`, `RecentFileEntry`, `CommandLinePdfArgs`, `PageRenderPlanner`, `ScrollPagePicker`, `PageRasterCache<T>`, `PrintPageRange`, `PdfUnlockPolicy`. `ClearPDF.Tests` references Core only, so `dotnet test` runs on Linux. The WPF app (`net8.0-windows`) references Core and still requires Windows to build/run the shell.

## v1 scope

**In**

- Open PDF (file dialog + recent list + command-line / Explorer “Open with” + window drag-drop)
- Render pages (page hero + left thumbnails)
- Scroll / PageUp·PageDown / Home·End
- Zoom in/out, fit width, fit page
- Find next/prev (extracted text)
- Print, Save As (copy file)
- Password prompt for encrypted PDFs (unlock for viewing only)
- Error banner UI
- Bookmarks/outline when the engine exposes them (stubbed TODO if Docnet has no outline API)

**Out**

- Forms, annotations, DRM/IRM beyond open-password
- JavaScript, OCR, sync/cloud accounts
- Ribbons / AI features

## v2 (ideas)

- Proper outline/bookmarks panel
- Text selection / copy
- Continuous vs single-page modes polish
- Tile / progressive decode for very large pages
- Optional dark canvas

## Layout map

| Screen | Mock | Code |
|--------|------|------|
| Empty + Recent | `docs/mocks/chrome-arc-empty.png` (v1.1) / `01-empty-open.png` | `EmptyView` in `MainWindow.xaml` |
| Reader | `docs/mocks/chrome-arc-reader.png` (v1.1) / `02-reader.png` | `ReaderView` + one icon toolbar + status bar |
| Password | `docs/mocks/03-password.png` | `Views/PasswordDialog.xaml` |

Accent: `#5A7FA6` (one muted blue — selected thumb + find hit). Canvas: `#F1F2F7` (sampled from `chrome-arc-reader.png` so the white page pops). Toolbar: one ~36px icon strip (Segoe MDL2), labels on hover. No ribbons.

## Project tree

```
ClearPDF/
  ClearPDF.sln
  LICENSE
  README.md
  .gitignore
  docs/mocks/
  docs/test-output.txt   # captured `dotnet test` on Linux
  src/ClearPDF.Core/     # net8.0 — ZoomHelper, FindHelper, RecentFiles, PdfPageSize, PdfRenderScale, CommandLinePdfArgs, PageRenderPlanner, PrintPageRange, PdfUnlockPolicy
  src/ClearPDF/          # net8.0-windows WPF shell (refs Core)
    ClearPDF.csproj
    App.xaml
    MainWindow.xaml
    Models/              BookmarkItem
    Services/            Docnet + stub, AppLog (local file logs)
    Views/               PasswordDialog
  tests/ClearPDF.Tests/  # net8.0 → Core only
```
