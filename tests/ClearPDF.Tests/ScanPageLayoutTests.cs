using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class ScanPageLayoutTests
{
    [Theory]
    [InlineData(2550, 3300, 300, 612, 792)]
    [InlineData(1275, 1650, 150, 612, 792)]
    [InlineData(1700, 2200, 200, 612, 792)]
    [InlineData(1200, 1800, 300, 288, 432)]
    public void Page_size_follows_pixels_over_dpi(
        int width, int height, int dpi, double expectW, double expectH)
    {
        var page = ScanTestPages.Solid(width, height, dpi);
        var size = ScanPageLayout.PageSizePoints(page);
        Assert.Equal(expectW, size.Width, precision: 5);
        Assert.Equal(expectH, size.Height, precision: 5);
    }

    [Fact]
    public void Rotate_90_swaps_page_size()
    {
        var page = ScanTestPages.Solid(2550, 3300, 300).RotateBy(90);
        var size = ScanPageLayout.PageSizePoints(page);
        Assert.Equal(792, size.Width, precision: 5);
        Assert.Equal(612, size.Height, precision: 5);
    }
}
