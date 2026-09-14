using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

/// <summary>
/// Documents the contract: GetPageSize / fit / print use PDF points (1/72"),
/// not Docnet pixel dimensions under PageDimensions(DocnetRenderScale).
/// </summary>
public class PdfPageSizeTests
{
    [Fact]
    public void FromDocnetPixels_letter_at_2x_scale_is_612_by_792_points()
    {
        // US Letter = 8.5×11" → 612×792 points. At DocnetRenderScale 2.0, PDFium
        // reports 1224×1584 pixels; callers must convert before FitWidth/FitPage.
        var (w, h) = PdfPageSize.FromDocnetPixels(
            widthPixels: PdfPageSize.LetterWidthPoints * PdfPageSize.DocnetRenderScale,
            heightPixels: PdfPageSize.LetterHeightPoints * PdfPageSize.DocnetRenderScale);

        Assert.Equal(612, w, precision: 5);
        Assert.Equal(792, h, precision: 5);
    }

    [Fact]
    public void Stub_and_letter_constants_match_PDF_points_contract()
    {
        Assert.Equal(612, PdfPageSize.LetterWidthPoints);
        Assert.Equal(792, PdfPageSize.LetterHeightPoints);
        Assert.Equal(2.0, PdfPageSize.DocnetRenderScale);
    }

    [Fact]
    public void FitWidth_uses_points_not_2x_pixels()
    {
        // If someone passed pixels (1224) instead of points (612), fit would be ~0.5× too small.
        var correct = ZoomHelper.FitWidth(pageWidth: 612, viewportWidth: 636, padding: 24);
        var wrongIfPixels = ZoomHelper.FitWidth(pageWidth: 1224, viewportWidth: 636, padding: 24);
        Assert.Equal(1.0, correct, precision: 5);
        Assert.True(wrongIfPixels < 0.55);
    }
}
