---
title: ClearPDF macOS Avalonia port (osx-x64)
date: 2026-10-31
artifact_contract: ce-unified-plan/v1
product_contract_source: ce-plan-bootstrap
execution: code
---

# ClearPDF macOS Avalonia port

Origin: uploaded `MACOS_PORT_PLAN.md` (scope survey) plus the implementation request. Base: `main` @ e3c4667 (v1.2.0).

## Goal Capsule

**Objective.** An Intel Mac on Sequoia 15.7 can open an unsigned `ClearPDF.app` (osx-x64) that reads PDFs with the same main-window features as Windows (open, recent, render, zoom, find, thumbs/multi-select, Save As / Save pages as, password, duck-waffles). Printing stays Windows-only.

**Means.** Keep `ClearPDF.Core` as the shared net8.0 library. Introduce a platform-neutral BGRA32 `PdfPixelBuffer`. Move `IPdfDocument`, `DocnetPdfDocument`, and `PageBitmapCache` onto that buffer so WPF and a new Avalonia 11 `ClearPDF.Mac` project share one engine. Bundle with `dotnet publish -r osx-x64 --self-contained` plus `Info.plist` / `.icns`. CI adds a macOS job; the existing Windows installer job stays unchanged.

**Stop if.** A settled decision is infeasible (Avalonia 11 cannot host the reader, or Docnet 2.6.0 has no `osx-x64` `pdfium.dylib`), or the Windows WPF compile would regress.

## Product Contract

### Requirements

- **R1.** `ClearPDF.Core` remains `net8.0` and UI-free except for the new pixel-buffer types.
- **R2.** `IPdfDocument.RenderPage`, `DocnetPdfDocument`, `StubPdfDocument`, and `PageBitmapCache` have no `BitmapSource` / WPF types. They use `PdfPixelBuffer` (BGRA32, width, height, stride).
- **R3.** Windows WPF converts buffers to frozen `BitmapSource` at the display/print boundary. Existing Windows behavior (render, cache, print) must not regress.
- **R4.** New Avalonia UI 11 app `src/ClearPDF.Mac` reproduces MainWindow features plus password and duck-waffles dialogs. Printing is hidden/disabled.
- **R5.** Docnet `osx-x64` `pdfium.dylib` is what the Mac build loads (no random native download). Probe natives on Windows and macOS; Linux stays on the stub.
- **R6.** Packaging: checked-in `.icns`, script that publishes self-contained osx-x64, assembles unsigned `ClearPDF.app` (Info.plist), zips it.
- **R7.** GitHub Actions macOS job builds, tests, uploads the `.app` zip. Existing `windows-latest` installer job stays green.
- **R8.** All existing tests pass. New tests cover `PdfPixelBuffer` and the buffer-backed `PageBitmapCache`.

### Actors

- **A1.** Steve on an Intel Mac (Sequoia 15.7): first-run Gatekeeper, then reads PDFs.
- **A2.** Windows user: unchanged WPF app.

### Key flows

- **F1.** Empty Open → recent list → open PDF (dialog, drop, argv).
- **F2.** Password-protected PDF → unlock dialog → view (password never stored).
- **F3.** Reader: thumbs, multi-select, zoom/fit, find, Save As choice, Save pages as.
- **F4.** About + Duck waffles.
- **F5.** Print exists only on Windows.

### Acceptance examples

- **AE1.** `dotnet test` on Linux/macOS: existing Core tests + new pixel-buffer tests pass.
- **AE2.** Mac job artifact is a zip containing `ClearPDF.app` built for osx-x64.
- **AE3.** First launch on Steve's Mac: right-click → Open (unsigned). No print control in the Mac UI.

### Scope

**In.** Shared pixel buffer, Core move of document/cache, Avalonia Mac shell, unsigned .app zip, macOS CI job, pixel-buffer tests.

**Out.** Mac printing, Apple signing/notarization, DMG, arm64/universal binary, replacing the WPF shell, QA-gate Mac soak procedure.

## Planning Contract

### Key Technical Decisions

- **KTD1. Pixel buffer lives in Core as a class.** `PdfPixelBuffer` is a sealed class (not a struct) so `PageRasterCache<T> where T : class` can cache it. `(session-settled: user-directed — chosen over keeping BitmapSource on IPdfDocument: both WPF and Avalonia must consume one raster type)`
- **KTD2. Document + cache move into Core, not a second Pdfium project.** One `IPdfDocument` / `DocnetPdfDocument` / `PageBitmapCache` used by both UIs. Accept a `Docnet.Core` 2.6.0 package on Core. `(session-settled: user-directed — chosen over a Mac-only raster fork: Windows must keep using the same types)`
- **KTD3. Avalonia 11 Mac project (`ClearPDF.Mac`).** Separate from WPF. Printing controls omitted. Cmd/Ctrl shortcuts for Open and Find; no print shortcut. `(session-settled: user-directed — chosen over MAUI/Uno/SwiftUI: Avalonia XAML is the closest WPF port; printing deferred)`
- **KTD4. Unsigned .app zip only.** Script + checked-in `.icns` + Info.plist. No notarize secrets. `(session-settled: user-directed — chosen over DMG/signed build: first test build is unsigned)`
- **KTD5. macOS CI as a new workflow file.** `macos-latest` publishes `-r osx-x64` (cross-compile from Apple Silicon is valid). macos-13 is retired; do not edit the Windows job. `(session-settled: user-directed — chosen over only-Windows CI: Mac artifact must be produced in Actions)`

### Assumptions

- macos-latest can restore Docnet's `runtimes/osx-x64/native/pdfium.dylib` and publish an Intel binary.
- Segoe MDL2 icons are unavailable on Mac; toolbar uses text/Unicode equivalents. Chrome tokens stay `#F1F2F7` / `#5A7FA6`.
- Linux `dotnet test` cannot compile WPF; Windows CI remains the WPF compile proof.

### Technical design

1. Core: `PdfPixelBuffer` + tests. Move `IPdfDocument`, exceptions, `BookmarkItem`, `DocnetPdfDocument`/`Stub`/`Factory`, `PageBitmapCache`, `AppLog`. `RenderPage` returns `PdfPixelBuffer?`. `ProbeNatives` true on Windows and OSX.
2. WPF: `PdfPixelBufferWpf.ToFrozenBitmap`. MainWindow and `PdfPrintPaginator` convert at the Image/print boundary. Drop WPF usings from moved files.
3. Mac: Avalonia 11.2.x app with MainWindow, PasswordDialog, DuckWafflesWindow. StorageProvider dialogs. Hide print.
4. `packaging/macos/{Info.plist,ClearPDF.icns,bundle-app.sh}`. Workflow `.github/workflows/macos.yml` artifact `ClearPDF-macos-x64-app`.

## Implementation Units

### U1. Pixel-buffer abstraction + Core tests

- Add `src/ClearPDF.Core/Imaging/PdfPixelBuffer.cs`.
- Add `tests/ClearPDF.Tests/PdfPixelBufferTests.cs` and `PageBitmapCacheTests.cs`.
- Covers R1, R2, R8.

### U2. Move document/cache onto the buffer

- Move and retarget `IPdfDocument`, `DocnetPdfDocument`, `PageBitmapCache`, `BookmarkItem`, `AppLog` into Core.
- WPF converts buffers; print paginator still works on Windows.
- Covers R2, R3, R5.

### U3. Avalonia Mac app

- `src/ClearPDF.Mac` with MainWindow + password + duck-waffles. Print hidden.
- Covers R4, R5.

### U4. Bundle script, icns, CI, README

- Packaging + `.github/workflows/macos.yml` + short README Mac section.
- Covers R6, R7.

## Verification Contract

```bash
dotnet test tests/ClearPDF.Tests/ClearPDF.Tests.csproj
dotnet build src/ClearPDF.Mac/ClearPDF.Mac.csproj
# packaging script syntax: bash -n packaging/macos/bundle-app.sh
```

Windows compile is proven by the existing `installer` job. Mac job must test + publish + upload zip.

## Definition of Done

- Existing tests green; new pixel-buffer/cache tests green.
- WPF project no longer types `IPdfDocument` on `BitmapSource`.
- Mac UI has no print entry points.
- PR open with first-launch and unsigned/no-print notes. Not merged.
