using System.Runtime.InteropServices;
using ClearPDF.Services;
using Xunit;

namespace ClearPDF.Tests;

public class PdfFixtureRenderTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "solid-red-72.pdf");

    [Fact]
    public void Renders_solid_red_fixture_when_pdfium_is_present()
    {
        if (!DocnetPdfDocumentFactory.AreNativesAvailable)
        {
            Assert.False(
                DocnetPdfDocumentFactory.RequiresNativePdfEngine,
                "PDFium must load on Windows and macOS — silent stub pages are a QA reject.");
            return;
        }

        using var doc = new DocnetPdfDocumentFactory().Open(FixturePath);
        Assert.Equal(1, doc.PageCount);

        var (wPts, hPts) = doc.GetPageSize(0);
        Assert.Equal(72, wPts, 0.5);
        Assert.Equal(72, hPts, 0.5);

        var buffer = doc.RenderPage(0, 1.0);
        Assert.NotNull(buffer);
        Assert.Equal(144, buffer!.Width);
        Assert.Equal(144, buffer.Height);
        Assert.Equal(144 * 4, buffer.Stride);

        // Center pixel of a DeviceRGB red fill — PDFium BGRA32.
        var i = (72 * buffer.Stride) + (72 * 4);
        Assert.Equal(0x00, buffer.Bgra32[i]);
        Assert.Equal(0x00, buffer.Bgra32[i + 1]);
        Assert.Equal(0xFF, buffer.Bgra32[i + 2]);
        Assert.Equal(0xFF, buffer.Bgra32[i + 3]);
    }

    [Fact]
    public void Missing_natives_on_required_platform_throws_instead_of_stub()
    {
        var ex = Assert.Throws<PdfOpenException>(() =>
            DocnetPdfDocumentFactory.FallbackWhenNativesMissing("doc.pdf", requireNatives: true));
        Assert.Equal(DocnetPdfDocumentFactory.MissingNativesMessage, ex.Message);
    }

    [Fact]
    public void Missing_natives_on_optional_platform_uses_stub()
    {
        using var doc = DocnetPdfDocumentFactory.FallbackWhenNativesMissing(
            "doc.pdf",
            requireNatives: false);
        Assert.Equal(3, doc.PageCount);
        var stub = doc.RenderPage(0, 0.2);
        Assert.NotNull(stub);
        Assert.Equal(0xCC, stub!.Bgra32[0]);
    }

    [Fact]
    public void Windows_and_macos_require_native_engine()
    {
        var required = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                       || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        Assert.Equal(required, DocnetPdfDocumentFactory.RequiresNativePdfEngine);
    }
}
