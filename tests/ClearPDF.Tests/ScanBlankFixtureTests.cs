using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class ScanBlankFixtureTests
{
    [Fact]
    public void Single_text_line_is_kept()
    {
        var rgb = ScanBlankDetectorTests.Solid(200, 260, 252, 252, 252);
        ScanBlankDetectorTests.DrawRect(rgb, 200, 20, 130, 160, 2, 20, 20, 20);
        Assert.False(ScanBlankDetector.IsBlank(rgb, 200, 260));
    }

    [Fact]
    public void Signature_is_kept()
    {
        var rgb = ScanBlankDetectorTests.Solid(200, 260, 252, 252, 252);
        DrawStroke(rgb, 200, 40, 180, 90, 188, 25, 25, 25);
        DrawStroke(rgb, 200, 90, 188, 130, 170, 25, 25, 25);
        DrawStroke(rgb, 200, 70, 176, 110, 200, 25, 25, 25);
        Assert.False(ScanBlankDetector.IsBlank(rgb, 200, 260));
    }

    [Fact]
    public void Faint_pencil_mark_is_kept()
    {
        var rgb = ScanBlankDetectorTests.Solid(200, 260, 252, 252, 252);
        ScanBlankDetectorTests.DrawRect(rgb, 200, 90, 200, 28, 3, 210, 210, 210);
        Assert.False(ScanBlankDetector.IsBlank(rgb, 200, 260));
    }

    [Fact]
    public void White_page_is_still_blank()
    {
        var rgb = ScanBlankDetectorTests.Solid(200, 260, 252, 252, 252);
        Assert.True(ScanBlankDetector.IsBlank(rgb, 200, 260));
    }

    private static void DrawStroke(
        byte[] rgb, int width, int x0, int y0, int x1, int y1, byte r, byte g, byte b)
    {
        var steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (var i = 0; i <= steps; i++)
        {
            var x = x0 + (x1 - x0) * i / steps;
            var y = y0 + (y1 - y0) * i / steps;
            for (var dy = -1; dy <= 1; dy++)
            {
                var yy = y + dy;
                if ((uint)x >= (uint)width || yy < 0)
                    continue;
                var p = (yy * width + x) * 3;
                if (p + 2 >= rgb.Length)
                    continue;
                rgb[p] = r;
                rgb[p + 1] = g;
                rgb[p + 2] = b;
            }
        }
    }
}
