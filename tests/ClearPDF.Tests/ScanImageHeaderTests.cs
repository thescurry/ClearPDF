using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class ScanImageHeaderTests
{
    [Fact]
    public void Solid_mock_page_is_a_jpeg()
    {
        var page = MockScanner.CreateSolidPage(2550, 3300, 300, ScanColorMode.Color, 12, 40, 80);
        Assert.Equal(ScanImageFormat.Jpeg, page.Format);
        Assert.True(ScanImageHeader.TryRead(page.ImageBytes, out var w, out var h, out var format));
        Assert.Equal(ScanImageFormat.Jpeg, format);
        Assert.Equal(16, w);
        Assert.Equal(16, h);
        Assert.Equal(2550, page.PixelWidth);
        Assert.Equal(3300, page.PixelHeight);
    }
}
