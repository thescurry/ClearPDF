using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class ScanPagePipelineTests
{
    [Fact]
    public void Hides_blank_pages_instead_of_discarding_them()
    {
        var blank = Raster(64, 80, 250, 250, 250);
        var processed = ScanPagePipeline.Process(blank, new ScanPipelineOptions
        {
            DropBlankPages = true,
            AutoDeskew = false,
            AutoTrim = false
        });
        Assert.NotNull(processed);
        Assert.True(processed.HiddenAsBlank);
        Assert.Same(blank.ImageBytes, processed.ImageBytes);
    }

    [Fact]
    public void Keeps_blank_pages_when_toggle_is_off()
    {
        var blank = Raster(64, 80, 250, 250, 250);
        var kept = ScanPagePipeline.Process(blank, new ScanPipelineOptions
        {
            DropBlankPages = false,
            AutoDeskew = false,
            AutoTrim = false
        });
        Assert.NotNull(kept);
    }

    [Fact]
    public void Auto_trim_shrinks_to_the_ink()
    {
        var rgb = ScanBlankDetectorTests.Solid(80, 100, 250, 250, 250);
        ScanBlankDetectorTests.DrawRect(rgb, 80, 30, 35, 20, 25, 15, 15, 15);
        var page = MockScanner.CreateRasterPage(80, 100, 150, ScanColorMode.Color, rgb);

        var trimmed = ScanPagePipeline.Process(page, new ScanPipelineOptions
        {
            DropBlankPages = false,
            AutoDeskew = false,
            AutoTrim = true
        });

        Assert.NotNull(trimmed);
        Assert.True(trimmed!.PixelWidth < 80);
        Assert.True(trimmed.PixelHeight < 100);
        Assert.True(trimmed.PixelWidth >= 20);
        Assert.True(trimmed.PixelHeight >= 25);
    }

    [Fact]
    public void Manual_crop_uses_the_normalized_rect()
    {
        var rgb = ScanBlankDetectorTests.Solid(40, 40, 20, 40, 80);
        var page = MockScanner.CreateRasterPage(40, 40, 150, ScanColorMode.Color, rgb);
        var cropped = ScanPagePipeline.Crop(page, new ScanCropRect(0.25, 0.25, 0.75, 0.75));
        Assert.Equal(20, cropped.PixelWidth);
        Assert.Equal(20, cropped.PixelHeight);
    }

    [Fact]
    public void Deskew_option_records_the_correction()
    {
        var rgb = ScanBlankDetectorTests.Solid(80, 80, 250, 250, 250);
        ScanBlankDetectorTests.DrawRect(rgb, 80, 10, 36, 60, 8, 10, 10, 10);
        var skewed = ScanRaster.Rotate(rgb, 80, 80, 8);
        var page = MockScanner.CreateRasterPage(
            skewed.Width, skewed.Height, 150, ScanColorMode.Color, skewed.Rgb);

        var processed = ScanPagePipeline.Process(page, new ScanPipelineOptions
        {
            DropBlankPages = false,
            AutoDeskew = true,
            AutoTrim = false
        });

        Assert.NotNull(processed);
        Assert.InRange(Math.Abs(processed!.DeskewDegrees), 5, 11);
    }

    private static ScannedPage Raster(int w, int h, byte r, byte g, byte b) =>
        MockScanner.CreateRasterPage(w, h, 150, ScanColorMode.Color,
            ScanBlankDetectorTests.Solid(w, h, r, g, b));
}
