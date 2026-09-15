using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class PageRasterCacheTests
{
    [Fact]
    public void ClearPages_leaves_thumbs()
    {
        var cache = new PageRasterCache<string>(maxPageEntries: 8);
        cache.Set(0, zoomKey: 20, "page0");
        cache.SetThumb(0, "thumb0");
        cache.ClearPages();

        Assert.Equal(0, cache.PageEntryCount);
        Assert.True(cache.TryGetThumb(0, out var thumb));
        Assert.Equal("thumb0", thumb);
        Assert.False(cache.TryGet(0, 20, out _));
    }

    [Fact]
    public void Trim_evicts_farthest_from_focus_not_thumbs()
    {
        var cache = new PageRasterCache<string>(maxPageEntries: 3);
        cache.FocusPage = 5;
        cache.Set(0, 20, "far");
        cache.Set(4, 20, "near4");
        cache.Set(5, 20, "cur");
        cache.Set(6, 20, "near6");
        cache.SetThumb(0, "thumb-far");

        Assert.Equal(3, cache.PageEntryCount);
        Assert.False(cache.TryGet(0, 20, out _));
        Assert.True(cache.TryGet(5, 20, out var cur));
        Assert.Equal("cur", cur);
        Assert.True(cache.TryGetThumb(0, out var thumb));
        Assert.Equal("thumb-far", thumb);
    }

    [Fact]
    public void Zoom_keys_do_not_clobber_thumbs()
    {
        var cache = new PageRasterCache<string>();
        var pageKey = PdfRenderScale.ZoomCacheKey(1.0);
        cache.Set(1, pageKey, "sharp");
        cache.SetThumb(1, "thumb");

        Assert.True(cache.TryGet(1, pageKey, out var page));
        Assert.True(cache.TryGetThumb(1, out var thumb));
        Assert.Equal("sharp", page);
        Assert.Equal("thumb", thumb);
        Assert.NotEqual(PdfRenderScale.ThumbCacheKey(), pageKey);
    }

    [Fact]
    public void Clear_drops_everything()
    {
        var cache = new PageRasterCache<string>();
        cache.Set(0, 1, "p");
        cache.SetThumb(0, "t");
        cache.Clear();
        Assert.Equal(0, cache.PageEntryCount);
        Assert.Equal(0, cache.ThumbCount);
    }
}
