using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class ZoomHelperTests
{
    [Theory]
    [InlineData(0.1, 0.25)]
    [InlineData(1.0, 1.0)]
    [InlineData(10.0, 4.0)]
    public void Clamp_respects_min_max(double input, double expected)
    {
        Assert.Equal(expected, ZoomHelper.Clamp(input));
    }

    [Fact]
    public void ZoomIn_then_ZoomOut_round_trips_near_default()
    {
        var z = ZoomHelper.ZoomIn(1.0);
        Assert.True(z > 1.0);
        var back = ZoomHelper.ZoomOut(z);
        Assert.Equal(1.0, back, precision: 5);
    }

    [Fact]
    public void FitWidth_scales_to_viewport()
    {
        var z = ZoomHelper.FitWidth(pageWidth: 612, viewportWidth: 636, padding: 24);
        Assert.Equal(1.0, z, precision: 5);
    }

    [Fact]
    public void FitPage_picks_limiting_dimension()
    {
        var z = ZoomHelper.FitPage(612, 792, viewportWidth: 636, viewportHeight: 400, padding: 24);
        // height-limited: (400-24)/792 ≈ 0.475
        Assert.InRange(z, 0.45, 0.50);
    }
}
