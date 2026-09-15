using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class ScrollPagePickerTests
{
    [Fact]
    public void PickCurrentPage_nearest_top_to_center()
    {
        var tops = new[] { 0.0, 800.0, 1600.0, 2400.0 };
        Assert.Equal(0, ScrollPagePicker.PickCurrentPage(tops, viewportCenterY: 100));
        Assert.Equal(1, ScrollPagePicker.PickCurrentPage(tops, viewportCenterY: 820));
        Assert.Equal(3, ScrollPagePicker.PickCurrentPage(tops, viewportCenterY: 2500));
    }

    [Fact]
    public void PickCurrentPageNear_uses_local_window()
    {
        var tops = new[] { 0.0, 800.0, 1600.0, 2400.0, 3200.0 };
        Assert.Equal(2, ScrollPagePicker.PickCurrentPageNear(tops, 1610, hint: 2));
        Assert.Equal(3, ScrollPagePicker.PickCurrentPageNear(tops, 2410, hint: 2));
    }

    [Fact]
    public void PickCurrentPageNear_falls_back_when_fling_jumps()
    {
        var tops = new[] { 0.0, 800.0, 1600.0, 2400.0, 3200.0, 4000.0 };
        // Hint is page 0 but viewport is already on page 5.
        Assert.Equal(5, ScrollPagePicker.PickCurrentPageNear(tops, 4010, hint: 0));
    }

    [Fact]
    public void Empty_list_returns_zero()
    {
        Assert.Equal(0, ScrollPagePicker.PickCurrentPage(Array.Empty<double>(), 10));
        Assert.Equal(0, ScrollPagePicker.PickCurrentPageNear(Array.Empty<double>(), 10, 3));
    }
}
