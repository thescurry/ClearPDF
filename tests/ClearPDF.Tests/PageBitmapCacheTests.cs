using ClearPDF.Helpers;
using ClearPDF.Imaging;
using ClearPDF.Services;
using Xunit;

namespace ClearPDF.Tests;

public class PageBitmapCacheTests
{
    private static PdfPixelBuffer Buffer(byte tag) =>
        new(new[] { tag, (byte)0, (byte)0, (byte)255 }, 1, 1);

    [Fact]
    public void Set_and_TryGet_round_trip_pixel_buffer()
    {
        var cache = new PageBitmapCache();
        var pixels = Buffer(1);
        cache.Set(2, 1.0, pixels);

        Assert.True(cache.TryGet(2, 1.0, out var found));
        Assert.Same(pixels, found);
    }

    [Fact]
    public void Set_rejects_null_buffer()
    {
        var cache = new PageBitmapCache();
        Assert.Throws<ArgumentNullException>(() => cache.Set(0, 1.0, null!));
    }

    [Fact]
    public void Thumbs_are_independent_of_page_rasters()
    {
        var cache = new PageBitmapCache();
        var page = Buffer(2);
        var thumb = Buffer(3);
        cache.Set(1, 1.0, page);
        cache.SetThumb(1, thumb);

        Assert.True(cache.TryGet(1, 1.0, out var foundPage));
        Assert.True(cache.TryGetThumb(1, out var foundThumb));
        Assert.Same(page, foundPage);
        Assert.Same(thumb, foundThumb);
        Assert.NotEqual(PdfRenderScale.ThumbCacheKey(), PdfRenderScale.ZoomCacheKey(1.0));
    }

    [Fact]
    public void ClearPages_leaves_thumbs()
    {
        var cache = new PageBitmapCache();
        cache.Set(0, 1.0, Buffer(4));
        cache.SetThumb(0, Buffer(5));
        cache.ClearPages();

        Assert.False(cache.TryGet(0, 1.0, out _));
        Assert.True(cache.TryGetThumb(0, out var thumb));
        Assert.Equal(5, thumb!.Bgra32[0]);
    }

    [Fact]
    public void Clear_drops_pages_and_thumbs()
    {
        var cache = new PageBitmapCache();
        cache.Set(0, 1.0, Buffer(6));
        cache.SetThumb(0, Buffer(7));
        cache.Clear();

        Assert.False(cache.TryGet(0, 1.0, out _));
        Assert.False(cache.TryGetThumb(0, out _));
    }
}
