using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class ScanBlankDetectorTests
{
    [Fact]
    public void White_page_is_blank()
    {
        var rgb = Solid(80, 100, 252, 252, 252);
        Assert.True(ScanBlankDetector.IsBlank(rgb, 80, 100));
    }

    [Fact]
    public void Page_with_ink_is_not_blank()
    {
        var rgb = Solid(80, 100, 252, 252, 252);
        DrawRect(rgb, 80, 20, 20, 40, 40, 10, 10, 10);
        Assert.False(ScanBlankDetector.IsBlank(rgb, 80, 100));
    }

    [Fact]
    public void ScannedPage_overload_uses_raster()
    {
        var blank = MockScanner.CreateRasterPage(64, 80, 150, ScanColorMode.Color, Solid(64, 80, 250, 250, 250));
        var ink = MockScanner.CreateRasterPage(64, 80, 150, ScanColorMode.Color, Solid(64, 80, 20, 20, 20));
        Assert.True(ScanBlankDetector.IsBlank(blank));
        Assert.False(ScanBlankDetector.IsBlank(ink));
    }

    internal static byte[] Solid(int width, int height, byte r, byte g, byte b)
    {
        var rgb = new byte[width * height * 3];
        for (var i = 0; i < rgb.Length; i += 3)
        {
            rgb[i] = r;
            rgb[i + 1] = g;
            rgb[i + 2] = b;
        }

        return rgb;
    }

    internal static void DrawRect(
        byte[] rgb, int width, int x0, int y0, int w, int h, byte r, byte g, byte b)
    {
        for (var y = y0; y < y0 + h; y++)
        {
            for (var x = x0; x < x0 + w; x++)
            {
                var i = (y * width + x) * 3;
                rgb[i] = r;
                rgb[i + 1] = g;
                rgb[i + 2] = b;
            }
        }
    }
}
