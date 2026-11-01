using ClearPDF.Helpers;
using ClearPDF.Services;
using Xunit;

namespace ClearPDF.Tests;

public class PdfPageTextTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "hello-clearpdf.pdf");

    [Fact]
    public void GetPageText_extracts_helvetica_string_when_pdfium_is_present()
    {
        if (!DocnetPdfDocumentFactory.AreNativesAvailable)
        {
            Assert.False(
                DocnetPdfDocumentFactory.RequiresNativePdfEngine,
                "PDFium must load on Windows and macOS — find depends on extracted text.");
            return;
        }

        using var doc = new DocnetPdfDocumentFactory().Open(FixturePath);
        var text = doc.GetPageText(0);
        Assert.Contains("Hello", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ClearPDF", text, StringComparison.OrdinalIgnoreCase);

        var hits = FindHelper.FindAll(new[] { text }, "clearpdf");
        Assert.NotEmpty(hits);
    }
}
