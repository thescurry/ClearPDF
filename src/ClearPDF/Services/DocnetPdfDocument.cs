using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClearPDF.Helpers;
using ClearPDF.Models;
using Docnet.Core;
using Docnet.Core.Models;

namespace ClearPDF.Services;

/// <summary>
/// Docnet.Core / PDFium-backed document. On Linux scaffold boxes (or missing
/// pdfium.dll), <see cref="DocnetPdfDocumentFactory"/> falls back to a stub.
/// <para>
/// Renders at target DPI via a short-lived reader over in-memory PDF bytes
/// (password used once to <c>Unlock</c> — never retained). Baseline reader is
/// kept only for page size / text. All native calls go through
/// <see cref="DocnetGate"/> because PDFium is not thread-safe.
/// </para>
/// </summary>
internal sealed class DocnetPdfDocument : IPdfDocument
{
    /// <summary>Serializes all Docnet/PDFium access process-wide.</summary>
    internal static readonly object DocnetGate = new();

    private readonly byte[] _pdfBytes;
    private readonly Docnet.Core.Readers.IDocReader _reader;
    private readonly (double Width, double Height)[] _pageSizes;
    private bool _disposed;

    public string FilePath { get; }
    public int PageCount { get; }
    public bool IsPasswordProtected { get; }

    public DocnetPdfDocument(
        Docnet.Core.Readers.IDocReader reader,
        byte[] pdfBytes,
        string path,
        bool passwordUsed)
    {
        _reader = reader;
        _pdfBytes = pdfBytes;
        FilePath = path;
        PageCount = reader.GetPageCount();
        IsPasswordProtected = passwordUsed;
        _pageSizes = new (double Width, double Height)[PageCount];

        lock (DocnetGate)
        {
            for (var i = 0; i < PageCount; i++)
            {
                using var page = _reader.GetPageReader(i);
                _pageSizes[i] = PdfPageSize.FromDocnetPixels(
                    page.GetPageWidth(),
                    page.GetPageHeight());
            }
        }
    }

    /// <summary>
    /// Rasterize <paramref name="pageIndex"/> at capped target DPI for
    /// <paramref name="zoom"/>. Safe to call from a worker thread; returns a
    /// frozen <see cref="BitmapSource"/>. Does not use TransformedBitmap.
    /// </summary>
    public BitmapSource? RenderPage(int pageIndex, double zoom)
    {
        EnsureNotDisposed();
        if (pageIndex < 0 || pageIndex >= PageCount)
            return null;

        var (wPts, hPts) = GetPageSize(pageIndex);
        var ppp = PdfRenderScale.CapPixelsPerPoint(zoom, wPts, hPts);

        byte[]? bytes;
        int width;
        int height;

        lock (DocnetGate)
        {
            EnsureNotDisposed();
            // Fresh reader at the capped pixels-per-point — no 2× + TransformedBitmap thrash.
            using var reader = DocLib.Instance.GetDocReader(_pdfBytes, new PageDimensions(ppp));
            using var page = reader.GetPageReader(pageIndex);
            bytes = page.GetImage();
            width = page.GetPageWidth();
            height = page.GetPageHeight();
        }

        if (bytes == null || width <= 0 || height <= 0)
            return null;

        var bmp = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            bytes,
            width * 4);
        bmp.Freeze();
        return bmp;
    }

    public (double Width, double Height) GetPageSize(int pageIndex)
    {
        EnsureNotDisposed();
        if (pageIndex < 0 || pageIndex >= PageCount)
            return (PdfPageSize.LetterWidthPoints, PdfPageSize.LetterHeightPoints);

        return _pageSizes[pageIndex];
    }

    public string GetPageText(int pageIndex)
    {
        EnsureNotDisposed();
        if (pageIndex < 0 || pageIndex >= PageCount)
            return string.Empty;

        lock (DocnetGate)
        {
            EnsureNotDisposed();
            using var page = _reader.GetPageReader(pageIndex);
            return page.GetText() ?? string.Empty;
        }
    }

    public IReadOnlyList<BookmarkItem> GetBookmarks()
    {
        EnsureNotDisposed();
        // Docnet.Core 2.x does not expose outline/bookmarks cleanly.
        // TODO(Win10): wire FPDF_Bookmark* or a Docnet version that exposes outline.
        return Array.Empty<BookmarkItem>();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        lock (DocnetGate)
        {
            _reader.Dispose();
        }
        // Drop unlocked bytes promptly; password was never retained.
        Array.Clear(_pdfBytes);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DocnetPdfDocument));
    }
}

/// <summary>
/// Placeholder when PDFium natives are unavailable (Linux scaffold / missing DLL).
/// Renders a gray page so the UI shell still works.
/// </summary>
internal sealed class StubPdfDocument : IPdfDocument
{
    public string FilePath { get; }
    public int PageCount => 3;
    public bool IsPasswordProtected => false;

    public StubPdfDocument(string path) => FilePath = path;

    public BitmapSource? RenderPage(int pageIndex, double zoom)
    {
        if (pageIndex < 0 || pageIndex >= PageCount)
            return null;

        // Thread-safe placeholder (no Visual/RenderTargetBitmap) so Task.Run render works.
        // Do not QuantizeZoom here — that clamps to UI MinZoom and would inflate thumb rasters.
        var ppp = PdfRenderScale.CapPixelsPerPoint(
            zoom,
            PdfPageSize.LetterWidthPoints,
            PdfPageSize.LetterHeightPoints);
        var w = Math.Max(1, (int)(PdfPageSize.LetterWidthPoints * ppp));
        var h = Math.Max(1, (int)(PdfPageSize.LetterHeightPoints * ppp));
        var stride = w * 4;
        var pixels = new byte[stride * h];
        for (var y = 0; y < h; y++)
        {
            var row = y * stride;
            for (var x = 0; x < w; x++)
            {
                var i = row + x * 4;
                var edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                if (edge)
                {
                    pixels[i] = 0xCC;     // B
                    pixels[i + 1] = 0xCC; // G
                    pixels[i + 2] = 0xCC; // R
                    pixels[i + 3] = 0xFF;
                }
                else
                {
                    pixels[i] = 0xFF;
                    pixels[i + 1] = 0xFF;
                    pixels[i + 2] = 0xFF;
                    pixels[i + 3] = 0xFF;
                }
            }
        }

        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bmp.Freeze();
        return bmp;
    }

    public (double Width, double Height) GetPageSize(int pageIndex) =>
        (PdfPageSize.LetterWidthPoints, PdfPageSize.LetterHeightPoints);

    public string GetPageText(int pageIndex) =>
        pageIndex >= 0 && pageIndex < PageCount
            ? $"Stub page {pageIndex + 1}. PDF preview uses Docnet.Core / PDFium on Windows."
            : string.Empty;

    public IReadOnlyList<BookmarkItem> GetBookmarks() => Array.Empty<BookmarkItem>();

    public void Dispose() { }
}

public sealed class DocnetPdfDocumentFactory : IPdfDocumentFactory
{
    private static readonly Lazy<bool> NativesAvailable = new(ProbeNatives);

    public static bool AreNativesAvailable => NativesAvailable.Value;

    public IPdfDocument Open(string path, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new PdfOpenException($"File not found: {path}");

        if (!AreNativesAvailable)
        {
            // Clean stub so the UI shell runs without pdfium.dll.
            return new StubPdfDocument(path);
        }

        try
        {
            var lib = DocLib.Instance;
            var usedPassword = !string.IsNullOrEmpty(password);
            // scalingFactor is pixels-per-point (PPI/72). DocnetRenderScale (2.0) ≈ 144 DPI.
            // GetPageSize divides by this factor to return PDF points. Per-zoom rasters use a
            // separate reader at CapPixelsPerPoint (see DocnetPdfDocument.RenderPage).
            var dims = new PageDimensions(PdfPageSize.DocnetRenderScale);

            byte[] pdfBytes;
            Docnet.Core.Readers.IDocReader reader;

            lock (DocnetPdfDocument.DocnetGate)
            {
                try
                {
                    if (usedPassword)
                    {
                        // Unlock once — password is not retained on the document instance.
                        pdfBytes = lib.Unlock(path, password!);
                    }
                    else
                    {
                        pdfBytes = File.ReadAllBytes(path);
                    }

                    reader = lib.GetDocReader(pdfBytes, dims);
                }
                catch (Exception ex) when (LooksLikePasswordError(ex))
                {
                    throw new PdfPasswordException(
                        "This PDF requires a password, or the password was incorrect.");
                }
            }

            return new DocnetPdfDocument(reader, pdfBytes, path, usedPassword);
        }
        catch (PdfPasswordException)
        {
            throw;
        }
        catch (DllNotFoundException)
        {
            return new StubPdfDocument(path);
        }
        catch (Exception ex)
        {
            throw new PdfOpenException($"Could not open PDF: {ex.Message}", ex);
        }
    }

    private static bool LooksLikePasswordError(Exception ex)
    {
        // PDFium FPDF_ERR_PASSWORD == 4; Docnet surfaces it on DocnetLoadDocumentException.ErrorCode.
        var codeProp = ex.GetType().GetProperty("ErrorCode");
        if (codeProp?.GetValue(ex) is uint code && code == 4)
            return true;

        var msg = (ex.Message ?? string.Empty) + " " + ex.GetType().Name;
        return msg.Contains("password", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("passwd", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ProbeNatives()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            _ = DocLib.Instance;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
