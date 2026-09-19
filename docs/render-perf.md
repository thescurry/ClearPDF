# Render performance (image-heavy PDFs)

## Confirmed pain

UI thread was blocked on Docnet `IPageReader.GetImage()` because `MainWindow.RenderAllPages` called `IPdfDocument.RenderPage` synchronously for every page on zoom settle. Scroll/`GoToPage` also called `BuildThumbnails()`, which re-ran Docnet at thumb zoom for **every** page.

`DocnetPdfDocument.RenderPage` opened the document at fixed `PageDimensions(2.0)` and applied further zoom with `TransformedBitmap` — soft upscales and memory thrash on full-bleed photo pages (e.g. Lamborghini brochure).

## What changed

1. **Target DPI render** — `RenderPage` uses in-memory PDF bytes + a short-lived `GetDocReader(..., new PageDimensions(cappedPixelsPerPoint))`. No `TransformedBitmap`.
2. **DPI caps** — `PdfRenderScale.CapPixelsPerPoint` clamps by max edge (4096) and max pixels (~8M).
3. **Bitmap cache** — `PageBitmapCache` keyed by `(pageIndex, quantizedZoom)` (`QuantizeStep` 0.05).
4. **Off UI thread** — rasters on `Task.Run`; UI only swaps frozen `Image.Source`. Last frame remains until the new bitmap arrives.
5. **Debounced zoom only** — pan/scroll update current-page chrome and ensure the sharp window; they do not rebuild all pages or thumbs.
6. **Virtualization (light)** — sharp for `current ± 2`; optional low-res (`PreviewZoom` 0.35) for far pages on open/zoom settle.
7. **Thumbnails once** — `BuildThumbnailShells` + async `ScheduleThumbFills` at `ThumbZoom` (0.2) with a **separate** thumb cache/generation so `CancelRenders` / scroll never abort or clear thumb bitmaps. Selection chrome (`RefreshThumbSelection`) diffs the multi-select set and never rebuilds Images. Shell rows are sized to page aspect via `GetPageSize` (width ~120px, height = width × h/w) with ~4px gaps — no fixed square/120×156 letterbox.
8. **Page shells sized in PDF points × zoom** — `Stretch.Fill` so capped rasters still layout correctly with Fit Width / Fit Page.
9. **Password** — `DocLib.Unlock` once; bytes kept for re-raster; password not stored on the document or disk. `AppLog` unchanged.

## v1.1 feel (tighten, same architecture)

Image-heavy fling still felt chunky because every page-crossing called `CancelRenders()` (dropping neighbor rasters) and `ScheduleThumbFills` flooded `Task.Run`. Thumbs were already a separate cache; zoom still must not rebuild them.

What tightened:

1. **Scroll does not cancel the sharp window** — `PageRenderPlanner.ShouldCancelInFlight` is false for in-window pages. Zoom/open still cancel-all.
2. **Scroll raster debounce** — chrome (thumb + status) updates immediately; sharp jobs wait `ScrollRenderDebounceMs` (80).
3. **Cached page tops** + `ScrollPagePicker.PickCurrentPageNear` — no `TransformToAncestor` per page per scroll tick.
4. **In-flight set** — do not enqueue the same `(page, zoom, gen)` twice.
5. **Bounded page cache** — `PageRasterCache<T>` evicts farthest-from-focus; `ClearPages` / zoom trim never drop thumbs.
6. **Sequential thumb fill** — one worker at `ThumbZoom`; does not compete with the current-page sharp raster.
7. **Preview never replaces sharp** — `ShouldKeepExistingBitmap`.
8. **Zoom debounce** 150 → 120ms. DPI caps unchanged (`MaxEdgePixels` 4096, `MaxPixels` ~8M, `QuantizeStep` 0.05).

Linux-tested helpers: `PageRenderPlanner`, `ScrollPagePicker`, `PageRasterCache<T>`, `PrintPageRange`, `PdfUnlockPolicy`, `PageSelection`, `PdfPageExtract`.

## Files

- `src/ClearPDF.Core/Helpers/PdfRenderScale.cs`
- `src/ClearPDF.Core/Helpers/PageRenderPlanner.cs`
- `src/ClearPDF.Core/Helpers/ScrollPagePicker.cs`
- `src/ClearPDF.Core/Helpers/PageRasterCache.cs`
- `src/ClearPDF/Services/PageBitmapCache.cs`
- `src/ClearPDF/Services/DocnetPdfDocument.cs`
- `src/ClearPDF/MainWindow.xaml.cs`
- `tests/ClearPDF.Tests/PdfRenderScaleTests.cs`
- `tests/ClearPDF.Tests/PageRenderPlannerTests.cs`
- `tests/ClearPDF.Tests/ScrollPagePickerTests.cs`
- `tests/ClearPDF.Tests/PageRasterCacheTests.cs`

## Deploy note

Parent deploys this tree to Win10 machine `5509bc64-03a5-4be9-8ac2-8427405e835d`. Friend zip is **not** part of this pass.
