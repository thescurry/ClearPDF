using System.IO;
using System.Runtime.InteropServices;
using ClearPDF.Helpers;
using ClearPDF.Imaging;
using ClearPDF.Models;
using Docnet.Core;
using Docnet.Core.Models;

namespace ClearPDF.Services;

/// <summary>
/// Docnet.Core / PDFium-backed document. On machines without natives,
/// <see cref="DocnetPdfDocumentFactory"/> falls back to a stub.
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
    /// <paramref name="zoom"/>. Safe to call from a worker thread.
    /// </summary>
    public PdfPixelBuffer? RenderPage(int pageIndex, double zoom)
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
            using var reader = DocLib.Instance.GetDocReader(_pdfBytes, new PageDimensions(ppp));
            using var page = reader.GetPageReader(pageIndex);
            bytes = page.GetImage();
            width = page.GetPageWidth();
            height = page.GetPageHeight();
        }

        if (bytes == null || width <= 0 || height <= 0)
            return null;

        return new PdfPixelBuffer(bytes, width, height);
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
        return Array.Empty<BookmarkItem>();
    }

    public void ExtractPages(IReadOnlyList<int> pageIndices0, string destPath)
    {
        EnsureNotDisposed();
        var pages = PdfPageExtract.Normalize(pageIndices0, PageCount);
        if (pages.Count == 0)
            throw new PdfOpenException("No pages to extract.");
        if (string.IsNullOrWhiteSpace(destPath))
            throw new PdfOpenException("Save path is empty.");

        var range = PdfPageExtract.ToDocnetPageRange(pages);
        byte[] bytes;
        lock (DocnetGate)
        {
            EnsureNotDisposed();
            bytes = DocLib.Instance.Split(_pdfBytes, range);
        }

        File.WriteAllBytes(destPath, bytes);
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

        Array.Clear(_pdfBytes);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DocnetPdfDocument));
    }
}

/// <summary>
/// Placeholder when PDFium natives are unavailable.
/// Renders a gray-edged white page so the UI shell still works.
/// </summary>
internal sealed class StubPdfDocument : IPdfDocument
{
    public string FilePath { get; }
    public int PageCount => 3;
    public bool IsPasswordProtected => false;

    public StubPdfDocument(string path) => FilePath = path;

    public PdfPixelBuffer? RenderPage(int pageIndex, double zoom)
    {
        if (pageIndex < 0 || pageIndex >= PageCount)
            return null;

        var ppp = PdfRenderScale.CapPixelsPerPoint(
            zoom,
            PdfPageSize.LetterWidthPoints,
            PdfPageSize.LetterHeightPoints);
        var w = Math.Max(1, (int)(PdfPageSize.LetterWidthPoints * ppp));
        var h = Math.Max(1, (int)(PdfPageSize.LetterHeightPoints * ppp));
        return PdfPixelBuffer.CreatePlaceholder(w, h);
    }

    public (double Width, double Height) GetPageSize(int pageIndex) =>
        (PdfPageSize.LetterWidthPoints, PdfPageSize.LetterHeightPoints);

    public string GetPageText(int pageIndex) =>
        pageIndex >= 0 && pageIndex < PageCount
            ? $"Stub page {pageIndex + 1}. PDF preview uses Docnet.Core / PDFium when natives are present."
            : string.Empty;

    public IReadOnlyList<BookmarkItem> GetBookmarks() => Array.Empty<BookmarkItem>();

    public void ExtractPages(IReadOnlyList<int> pageIndices0, string destPath) =>
        throw new PdfOpenException("PDFium natives not loaded — cannot extract pages.");

    public void Dispose() { }
}

public sealed class DocnetPdfDocumentFactory : IPdfDocumentFactory
{
    private static readonly Lazy<bool> NativesAvailable = new(ProbeNatives);

    public static bool AreNativesAvailable => NativesAvailable.Value;

    /// <summary>
    /// Windows and macOS ship PDFium. A load failure there is an error, not a
    /// silent gray stub. Linux scaffold boxes may still open a stub document.
    /// </summary>
    public static bool RequiresNativePdfEngine =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public const string MissingNativesMessage =
        "PDFium natives failed to load. On Windows, pdfium.dll must sit next to ClearPDF.exe. On macOS, pdfium.dylib must sit next to the ClearPDF executable inside ClearPDF.app.";

    public IPdfDocument Open(string path, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new PdfOpenException($"File not found: {path}");

        if (!AreNativesAvailable)
            return FallbackWhenNativesMissing(path, RequiresNativePdfEngine);

        try
        {
            var lib = DocLib.Instance;
            var usedPassword = !string.IsNullOrEmpty(password);
            var dims = new PageDimensions(PdfPageSize.DocnetRenderScale);

            byte[] pdfBytes;
            Docnet.Core.Readers.IDocReader reader;

            lock (DocnetPdfDocument.DocnetGate)
            {
                try
                {
                    if (usedPassword)
                    {
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
        catch (DllNotFoundException ex)
        {
            AppLog.Error(MissingNativesMessage, ex);
            return FallbackWhenNativesMissing(path, RequiresNativePdfEngine);
        }
        catch (Exception ex)
        {
            throw new PdfOpenException($"Could not open PDF: {ex.Message}", ex);
        }
    }

    /// <summary>Test seam for missing-native policy (throw vs stub).</summary>
    internal static IPdfDocument FallbackWhenNativesMissing(string path, bool requireNatives)
    {
        if (requireNatives)
        {
            AppLog.Error(MissingNativesMessage);
            throw new PdfOpenException(MissingNativesMessage);
        }

        AppLog.Warn("PDFium natives not loaded — using stub pages (non-Windows/macOS).");
        return new StubPdfDocument(path);
    }

    private static bool LooksLikePasswordError(Exception ex)
    {
        var codeProp = ex.GetType().GetProperty("ErrorCode");
        if (codeProp?.GetValue(ex) is uint code && code == 4)
            return true;

        var msg = (ex.Message ?? string.Empty) + " " + ex.GetType().Name;
        return msg.Contains("password", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("passwd", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ProbeNatives()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        try
        {
            _ = DocLib.Instance;
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("PDFium probe failed", ex);
            return false;
        }
    }
}
