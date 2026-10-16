using ClearPDF.Scanning;
using PdfSharp.Pdf.IO;
using Xunit;

namespace ClearPDF.Tests;

public class ScannedPdfBuilderTests
{
    [Fact]
    public void SaveNew_writes_page_count_order_and_letter_size_at_300dpi()
    {
        var pages = new[]
        {
            ScanTestPages.Solid(2550, 3300, 300, tag: 1),
            ScanTestPages.Solid(2550, 3300, 300, tag: 2),
            ScanTestPages.Solid(2550, 3300, 300, tag: 3)
        };
        var path = TempPdf();

        ScannedPdfBuilder.SaveNew(pages, path);

        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.Equal(3, doc.PageCount);
        Assert.Equal(612, doc.Pages[0].Width.Point, precision: 2);
        Assert.Equal(792, doc.Pages[0].Height.Point, precision: 2);
        Assert.Contains("/DCTDecode", File.ReadAllText(path).Replace('\0', ' '));
    }

    [Fact]
    public void SaveNew_keeps_scan_order_when_session_is_reordered()
    {
        var session = new ScanSession();
        session.AddRange(new[]
        {
            ScanTestPages.Solid(100, 100, 150, tag: 1),
            ScanTestPages.Solid(200, 100, 150, tag: 2),
            ScanTestPages.Solid(300, 100, 150, tag: 3)
        });
        session.Move(0, 2); // 2, 3, 1
        var path = TempPdf();

        ScannedPdfBuilder.SaveNew(session.Pages, path);

        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.Equal(3, doc.PageCount);
        Assert.Equal(ScanPageLayout.PageSizePoints(session.Pages[0]).Width, doc.Pages[0].Width.Point, precision: 2);
        Assert.Equal(ScanPageLayout.PageSizePoints(session.Pages[1]).Width, doc.Pages[1].Width.Point, precision: 2);
        Assert.Equal(ScanPageLayout.PageSizePoints(session.Pages[2]).Width, doc.Pages[2].Width.Point, precision: 2);
    }

    [Fact]
    public void SaveNew_rotated_page_swaps_dimensions()
    {
        var page = ScanTestPages.Solid(2550, 3300, 300).RotateBy(90);
        var path = TempPdf();
        ScannedPdfBuilder.SaveNew(new[] { page }, path);

        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.Equal(792, doc.Pages[0].Width.Point, precision: 2);
        Assert.Equal(612, doc.Pages[0].Height.Point, precision: 2);
    }

    [Fact]
    public void SaveNew_rejects_empty_session()
    {
        var path = TempPdf();
        Assert.Throws<ArgumentException>(() =>
            ScannedPdfBuilder.SaveNew(Array.Empty<ScannedPage>(), path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SaveNew_existing_dest_is_replaced_atomically()
    {
        var path = TempPdf();
        File.WriteAllText(path, "not-a-pdf");
        ScannedPdfBuilder.SaveNew(new[] { ScanTestPages.Solid(80, 80, 150, tag: 1) }, path);

        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public void Append_adds_pages_to_existing_pdf_in_order()
    {
        var path = TempPdf();
        ScannedPdfBuilder.SaveNew(new[] { ScanTestPages.Solid(2550, 3300, 300, tag: 1) }, path);
        var originalBytes = File.ReadAllBytes(path);

        ScannedPdfBuilder.Append(
            new[]
            {
                ScanTestPages.Solid(1275, 1650, 150, tag: 2),
                ScanTestPages.Solid(1275, 1650, 150, tag: 3)
            },
            sourcePdfPath: path);

        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.Equal(3, doc.PageCount);
        Assert.Equal(612, doc.Pages[0].Width.Point, precision: 2);
        Assert.Equal(612, doc.Pages[1].Width.Point, precision: 2);
        Assert.Equal(612, doc.Pages[2].Width.Point, precision: 2);
        // Source was replaced, not left as the original 1-page file.
        Assert.NotEqual(originalBytes.Length, new FileInfo(path).Length);
    }

    [Fact]
    public void Append_to_new_path_leaves_source_untouched()
    {
        var source = TempPdf();
        var dest = TempPdf();
        ScannedPdfBuilder.SaveNew(new[] { ScanTestPages.Solid(100, 100, 150, tag: 1) }, source);
        var sourceBytes = File.ReadAllBytes(source);

        ScannedPdfBuilder.Append(
            new[] { ScanTestPages.Solid(120, 120, 150, tag: 2) },
            sourcePdfPath: source,
            destinationPath: dest);

        Assert.Equal(sourceBytes, File.ReadAllBytes(source));
        using var srcDoc = PdfReader.Open(source, PdfDocumentOpenMode.Import);
        using var destDoc = PdfReader.Open(dest, PdfDocumentOpenMode.Import);
        Assert.Equal(1, srcDoc.PageCount);
        Assert.Equal(2, destDoc.PageCount);
    }

    [Fact]
    public void Append_failed_write_does_not_clobber_source()
    {
        var source = TempPdf();
        ScannedPdfBuilder.SaveNew(new[] { ScanTestPages.Solid(80, 80, 150, tag: 1) }, source);
        var before = File.ReadAllBytes(source);

        Assert.Throws<ArgumentException>(() =>
            ScannedPdfBuilder.Append(Array.Empty<ScannedPage>(), source));

        Assert.Equal(before, File.ReadAllBytes(source));
    }

    [Fact]
    public void Grayscale_pages_are_still_jpeg_compressed()
    {
        var page = MockScanner.CreateSolidPage(64, 64, 150, ScanColorMode.Grayscale, 20, 20, 20);
        var path = TempPdf();
        ScannedPdfBuilder.SaveNew(new[] { page }, path);
        var text = File.ReadAllText(path).Replace('\0', ' ');
        Assert.Contains("/DCTDecode", text);
    }

    private static string TempPdf()
    {
        var dir = Path.Combine(Path.GetTempPath(), "clearpdf-scan-tests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, Guid.NewGuid().ToString("N") + ".pdf");
    }
}
