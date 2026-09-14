using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class PdfRenderScaleTests
{
    [Fact]
    public void QuantizeZoom_rounds_to_step()
    {
        Assert.Equal(1.0, PdfRenderScale.QuantizeZoom(1.02), precision: 4);
        Assert.Equal(1.25, PdfRenderScale.QuantizeZoom(1.24), precision: 4);
        Assert.Equal(0.25, PdfRenderScale.QuantizeZoom(0.1), precision: 4);
        Assert.Equal(4.0, PdfRenderScale.QuantizeZoom(9), precision: 4);
    }

    [Fact]
    public void CapPixelsPerPoint_at_default_zoom_is_baseline_for_letter()
    {
        var ppp = PdfRenderScale.CapPixelsPerPoint(
            zoom: 1.0,
            pageWidthPoints: 612,
            pageHeightPoints: 792);

        Assert.Equal(PdfPageSize.DocnetRenderScale, ppp, precision: 5);
    }

    [Fact]
    public void CapPixelsPerPoint_limits_huge_zoom_on_photo_page()
    {
        // Full-bleed landscape photo page, zoomed in hard.
        var ppp = PdfRenderScale.CapPixelsPerPoint(
            zoom: 4.0,
            pageWidthPoints: 2000,
            pageHeightPoints: 1200);

        var w = 2000 * ppp;
        var h = 1200 * ppp;
        Assert.True(Math.Max(w, h) <= PdfRenderScale.MaxEdgePixels + 1);
        Assert.True(w * h <= PdfRenderScale.MaxPixels * 1.01);
        // Uncapped would be 4 * 2 = 8 ppp → 16000×9600; must be far below that.
        Assert.True(ppp < 4.0);
    }

    [Fact]
    public void ZoomCacheKey_stable_for_nearby_values()
    {
        var a = PdfRenderScale.ZoomCacheKey(1.0);
        var b = PdfRenderScale.ZoomCacheKey(1.02);
        Assert.Equal(a, b);
        Assert.NotEqual(a, PdfRenderScale.ZoomCacheKey(1.25));
    }

    [Fact]
    public void CapPixelsPerPoint_allows_thumb_zoom_below_ui_min()
    {
        var ppp = PdfRenderScale.CapPixelsPerPoint(
            zoom: PdfRenderScale.ThumbZoom,
            pageWidthPoints: 612,
            pageHeightPoints: 792);

        // ThumbZoom 0.2 * baseline 2.0 = 0.4 ppp — must not clamp up to MinZoom 0.25 → 0.5
        Assert.Equal(PdfRenderScale.ThumbZoom * PdfRenderScale.BaselinePixelsPerPoint, ppp, precision: 5);
        var w = 612 * ppp;
        // ~245px bitmap; UI shell width ~120px, height from page aspect (Stretch.Fill)
        Assert.InRange(w, 200, 280);
    }

    [Fact]
    public void ThumbCacheKey_differs_from_min_ui_zoom_key()
    {
        Assert.NotEqual(PdfRenderScale.ThumbCacheKey(), PdfRenderScale.ZoomCacheKey(ZoomHelper.MinZoom));
    }

}
