using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class ScanPageStoreTests
{
    [Fact]
    public void Persist_drops_the_full_raster_from_memory()
    {
        using var store = new ScanPageStore();
        var rgb = ScanBlankDetectorTests.Solid(240, 300, 20, 40, 80);
        var page = MockScanner.CreateRasterPage(240, 300, 150, ScanColorMode.Color, rgb);

        var held = store.Persist(page);

        Assert.Null(held.RasterRgb);
        Assert.False(string.IsNullOrWhiteSpace(held.RasterPath));
        Assert.True(File.Exists(held.RasterPath));
        Assert.NotNull(held.ThumbnailBytes);
        Assert.True(held.ThumbnailBytes!.Length < rgb.Length);
        Assert.True(ScanRaster.TryGetRgb(held, out var w, out var h, out var loaded));
        Assert.Equal(240, w);
        Assert.Equal(300, h);
        Assert.Equal(rgb.Length, loaded.Length);
    }

    [Fact]
    public void One_hundred_pages_keep_jpeg_and_thumb_only()
    {
        using var store = new ScanPageStore();
        const int width = 240;
        const int height = 300;
        var pages = new List<ScannedPage>(100);
        for (var i = 0; i < 100; i++)
        {
            var rgb = ScanBlankDetectorTests.Solid(width, height, (byte)(20 + i), 40, 80);
            var page = MockScanner.CreateRasterPage(width, height, 150, ScanColorMode.Color, rgb);
            pages.Add(store.Persist(ScanPagePipeline.Process(page, new ScanPipelineOptions
            {
                DropBlankPages = false,
                AutoDeskew = false,
                AutoTrim = false
            })));
        }

        Assert.Equal(100, pages.Count);
        Assert.All(pages, p => Assert.Null(p.RasterRgb));
        Assert.All(pages, p => Assert.True(File.Exists(p.RasterPath)));
        Assert.All(pages, p => Assert.NotNull(p.ThumbnailBytes));

        var inMemory = pages.Sum(p =>
            (p.ImageBytes?.Length ?? 0) + (p.ThumbnailBytes?.Length ?? 0));
        var rasterOnDisk = pages.Sum(p => new FileInfo(p.RasterPath!).Length);
        Assert.True(rasterOnDisk > 100 * 50_000, "full rasters should live on disk");
        Assert.True(inMemory < rasterOnDisk / 3, "RAM should be JPEG + thumb, not the full raster");
    }
}
