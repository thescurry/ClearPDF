using System.Runtime.InteropServices;
using ClearPDF.Helpers;
using Docnet.Core;
using Docnet.Core.Models;

namespace ClearPDF.Pdfium;

/// <summary>
/// Docnet.Core / PDFium-backed raster document. Shared by the Avalonia Mac
/// shell. Renders at target DPI via a short-lived reader over in-memory PDF
/// bytes (password used once to <c>Unlock</c> — never retained). All native
/// calls go through <see cref="DocnetGate"/> because PDFium is not thread-safe.
/// </summary>
internal sealed class DocnetPdfRasterDocument : IPdfRasterDocument
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

    public DocnetPdfRasterDocument(
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

    public PdfRaster? RenderPage(int pageIndex, double zoom)
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

        return new PdfRaster(bytes, width, height);
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
            throw new ObjectDisposedException(nameof(DocnetPdfRasterDocument));
    }
}

/// <summary>
/// Placeholder when PDFium natives are unavailable. Renders a gray page so
/// the Avalonia shell still opens on Linux scaffold boxes.
/// </summary>
internal sealed class StubPdfRasterDocument : IPdfRasterDocument
{
    public string FilePath { get; }
    public int PageCount => 3;
    public bool IsPasswordProtected => false;

    public StubPdfRasterDocument(string path) => FilePath = path;

    public PdfRaster? RenderPage(int pageIndex, double zoom)
    {
        if (pageIndex < 0 || pageIndex >= PageCount)
            return null;

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
                pixels[i] = edge ? (byte)0xCC : (byte)0xFF;
                pixels[i + 1] = edge ? (byte)0xCC : (byte)0xFF;
                pixels[i + 2] = edge ? (byte)0xCC : (byte)0xFF;
                pixels[i + 3] = 0xFF;
            }
        }

        return new PdfRaster(pixels, w, h);
    }

    public (double Width, double Height) GetPageSize(int pageIndex) =>
        (PdfPageSize.LetterWidthPoints, PdfPageSize.LetterHeightPoints);

    public string GetPageText(int pageIndex) =>
        pageIndex >= 0 && pageIndex < PageCount
            ? $"Stub page {pageIndex + 1}. PDF preview uses Docnet.Core / PDFium."
            : string.Empty;

    public void ExtractPages(IReadOnlyList<int> pageIndices0, string destPath) =>
        throw new PdfOpenException("PDFium natives not loaded — cannot extract pages.");

    public void Dispose() { }
}

public sealed class DocnetPdfRasterDocumentFactory : IPdfRasterDocumentFactory
{
    private static readonly Lazy<bool> NativesAvailable = new(ProbeNatives);

    public static bool AreNativesAvailable => NativesAvailable.Value;

    public IPdfRasterDocument Open(string path, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new PdfOpenException($"File not found: {path}");

        if (!AreNativesAvailable)
            return new StubPdfRasterDocument(path);

        try
        {
            var lib = DocLib.Instance;
            var usedPassword = !string.IsNullOrEmpty(password);
            var dims = new PageDimensions(PdfPageSize.DocnetRenderScale);

            byte[] pdfBytes;
            Docnet.Core.Readers.IDocReader reader;

            lock (DocnetPdfRasterDocument.DocnetGate)
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

            return new DocnetPdfRasterDocument(reader, pdfBytes, path, usedPassword);
        }
        catch (PdfPasswordException)
        {
            throw;
        }
        catch (DllNotFoundException)
        {
            return new StubPdfRasterDocument(path);
        }
        catch (Exception ex)
        {
            throw new PdfOpenException($"Could not open PDF: {ex.Message}", ex);
        }
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

    /// <summary>macOS .app bundles sometimes fail RID probing; look beside the exe.</summary>
    public static void HintNativeSearchPath(params string[] directories)
    {
        var dirs = directories
            .Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (dirs.Length == 0)
            return;

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(DocLib).Assembly, (name, _, _) =>
            {
                if (!name.Contains("pdfium", StringComparison.OrdinalIgnoreCase))
                    return IntPtr.Zero;

                foreach (var directory in dirs)
                {
                    foreach (var file in new[]
                    {
                        Path.Combine(directory, "pdfium.dylib"),
                        Path.Combine(directory, "libpdfium.dylib"),
                        Path.Combine(directory, "pdfium.so"),
                        Path.Combine(directory, "libpdfium.so"),
                        Path.Combine(directory, "pdfium.dll")
                    })
                    {
                        if (File.Exists(file) && NativeLibrary.TryLoad(file, out var handle))
                            return handle;
                    }
                }

                return IntPtr.Zero;
            });
        }
        catch
        {
            // Resolver already set — ignore.
        }
    }
}
