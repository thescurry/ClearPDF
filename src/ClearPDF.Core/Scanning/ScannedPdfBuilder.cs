using System.Text;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ClearPDF.Scanning;

/// <summary>
/// Builds a PDF of scanned page images with PDFsharp 6. Color/grayscale pages
/// are JPEG (/DCTDecode). Page size is pixels × 72 / DPI. Writes never land
/// on the destination until the temp file is complete.
/// </summary>
public static class ScannedPdfBuilder
{
    public static void SaveNew(IReadOnlyList<ScannedPage> pages, string destinationPath)
    {
        EnsurePages(pages);
        EnsureCodePages();
        AtomicFileReplace.Write(destinationPath, tmp =>
        {
            using var document = new PdfDocument();
            document.Info.Title = "Scanned document";
            document.Info.Creator = "ClearPDF";
            AddPages(document, pages);
            document.Save(tmp);
        });
    }

    /// <summary>
    /// Append scanned pages to an existing PDF. When
    /// <paramref name="destinationPath"/> is null or equals the source, the
    /// source is replaced atomically (temp file, then replace) — never written
    /// in place while the viewer still holds the path.
    /// </summary>
    public static void Append(
        IReadOnlyList<ScannedPage> pages,
        string sourcePdfPath,
        string? destinationPath = null)
    {
        EnsurePages(pages);
        if (string.IsNullOrWhiteSpace(sourcePdfPath))
            throw new ArgumentException("Source PDF path is required.", nameof(sourcePdfPath));
        if (!File.Exists(sourcePdfPath))
            throw new FileNotFoundException("Source PDF was not found.", sourcePdfPath);

        EnsureCodePages();
        var dest = string.IsNullOrWhiteSpace(destinationPath) ? sourcePdfPath : destinationPath;
        var sourceBytes = File.ReadAllBytes(sourcePdfPath);

        AtomicFileReplace.Write(dest, tmp =>
        {
            using var input = new MemoryStream(sourceBytes, writable: false);
            using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            AddPages(document, pages);
            document.Save(tmp);
        });
    }

    /// <summary>
    /// Insert scanned pages at <paramref name="insertAt"/> (0 = before the
    /// first existing page; <c>CountPages</c> = append).
    /// </summary>
    public static void Insert(
        IReadOnlyList<ScannedPage> pages,
        string sourcePdfPath,
        int insertAt,
        string? destinationPath = null)
    {
        EnsurePages(pages);
        if (string.IsNullOrWhiteSpace(sourcePdfPath))
            throw new ArgumentException("Source PDF path is required.", nameof(sourcePdfPath));
        if (!File.Exists(sourcePdfPath))
            throw new FileNotFoundException("Source PDF was not found.", sourcePdfPath);
        if (insertAt < 0)
            throw new ArgumentOutOfRangeException(nameof(insertAt));

        EnsureCodePages();
        var dest = string.IsNullOrWhiteSpace(destinationPath) ? sourcePdfPath : destinationPath;
        var sourceBytes = File.ReadAllBytes(sourcePdfPath);

        AtomicFileReplace.Write(dest, tmp =>
        {
            using var input = new MemoryStream(sourceBytes, writable: false);
            using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
            var at = Math.Clamp(insertAt, 0, source.PageCount);
            using var document = new PdfDocument();
            document.Info.Title = source.Info.Title;
            document.Info.Creator = "ClearPDF";
            for (var i = 0; i < at; i++)
                document.AddPage(source.Pages[i]);
            AddPages(document, pages);
            for (var i = at; i < source.PageCount; i++)
                document.AddPage(source.Pages[i]);
            document.Save(tmp);
        });
    }

    public static int CountPages(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return 0;
        EnsureCodePages();
        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        return document.PageCount;
    }

    public static int ResolveInsertIndex(ScanInsertMode mode, int afterPage1Based, int openPageCount)
    {
        var count = Math.Max(0, openPageCount);
        switch (mode)
        {
            case ScanInsertMode.Start:
                return 0;
            case ScanInsertMode.End:
                return count;
            case ScanInsertMode.AfterPage:
                return Math.Clamp(afterPage1Based, 0, count);
            default:
            {
                ScanInsertMode unreachable = mode;
                throw new ArgumentOutOfRangeException(nameof(mode), unreachable, null);
            }
        }
    }

    private static void AddPages(PdfDocument document, IReadOnlyList<ScannedPage> pages)
    {
        foreach (var page in pages)
        {
            var jpeg = EnsureJpeg(page);
            var (wPt, hPt) = ScanPageLayout.PageSizePoints(page);
            var pdfPage = document.AddPage();
            pdfPage.Width = XUnit.FromPoint(wPt);
            pdfPage.Height = XUnit.FromPoint(hPt);

            using var gfx = XGraphics.FromPdfPage(pdfPage);
            using var stream = new MemoryStream(jpeg, 0, jpeg.Length, writable: false, publiclyVisible: true);
            using var image = XImage.FromStream(stream);

            if (page.RotationDegrees == 0)
            {
                gfx.DrawImage(image, 0, 0, wPt, hPt);
                continue;
            }

            var dpi = Math.Max(page.Dpi, 1);
            var unrotatedW = page.PixelWidth * 72.0 / dpi;
            var unrotatedH = page.PixelHeight * 72.0 / dpi;
            gfx.TranslateTransform(wPt / 2, hPt / 2);
            gfx.RotateTransform(page.RotationDegrees);
            gfx.DrawImage(image, -unrotatedW / 2, -unrotatedH / 2, unrotatedW, unrotatedH);
        }
    }

    private static byte[] EnsureJpeg(ScannedPage page)
    {
        if (page.Format == ScanImageFormat.Jpeg && page.ImageBytes.Length >= 2 &&
            page.ImageBytes[0] == 0xFF && page.ImageBytes[1] == 0xD8)
            return page.ImageBytes;

        if (ScanBmp.TryDecodeRgb(page.ImageBytes, out var w, out var h, out var rgb))
            return ScanJpegEncoder.EncodeRgb(rgb, w, h, page.ColorMode);

        throw new InvalidOperationException("Scanned page is not a JPEG or BMP image.");
    }

    private static void EnsurePages(IReadOnlyList<ScannedPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Count == 0)
            throw new ArgumentException("No scanned pages to save.", nameof(pages));
    }

    private static void EnsureCodePages()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
