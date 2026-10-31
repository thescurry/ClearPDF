using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class ScanDeskewTests
{
    [Fact]
    public void Aligned_bar_stays_near_zero()
    {
        var rgb = BarPage(angle: 0);
        var angle = ScanDeskew.EstimateAngle(rgb, 80, 80);
        Assert.InRange(angle, -0.6, 0.6);
    }

    [Fact]
    public void Skewed_bar_is_detected_and_straightened()
    {
        var aligned = BarPage(angle: 0);
        var skewed = ScanRaster.Rotate(aligned, 80, 80, 8);
        var estimated = ScanDeskew.EstimateAngle(skewed.Rgb, skewed.Width, skewed.Height);
        Assert.InRange(estimated, 5, 11);

        var straight = ScanDeskew.Straighten(skewed.Rgb, skewed.Width, skewed.Height);
        Assert.InRange(Math.Abs(straight.AngleDegrees), 5, 11);
        var leftover = ScanDeskew.EstimateAngle(straight.Rgb, straight.Width, straight.Height);
        Assert.InRange(leftover, -2.0, 2.0);
    }

    [Fact]
    public void Uniform_page_is_not_rotated()
    {
        var rgb = ScanBlankDetectorTests.Solid(48, 48, 200, 200, 200);
        var result = ScanDeskew.Straighten(rgb, 48, 48);
        Assert.Equal(0, result.AngleDegrees);
        Assert.Same(rgb, result.Rgb);
    }

    private static byte[] BarPage(double angle)
    {
        var rgb = ScanBlankDetectorTests.Solid(80, 80, 250, 250, 250);
        ScanBlankDetectorTests.DrawRect(rgb, 80, 10, 36, 60, 8, 10, 10, 10);
        if (Math.Abs(angle) < 0.05)
            return rgb;
        return ScanRaster.Rotate(rgb, 80, 80, angle).Rgb;
    }
}
