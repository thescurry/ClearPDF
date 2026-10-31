namespace ClearPDF.Scanning;

/// <summary>
/// Per-page tidy: blank hide (never discard), optional deskew, margin trim,
/// then JPEG for PDF. Scanner I/O stays outside this type.
/// </summary>
public static class ScanPagePipeline
{
    public static ScannedPage Process(ScannedPage page, ScanPipelineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        options ??= new ScanPipelineOptions();

        if (!ScanRaster.TryGetRgb(page, out var width, out var height, out var rgb))
            return page;

        if (options.DropBlankPages && ScanBlankDetector.IsBlank(rgb, width, height))
        {
            return page with { HiddenAsBlank = true };
        }

        var geometryChanged = false;
        var angle = 0.0;
        if (options.AutoDeskew)
        {
            var straightened = ScanDeskew.Straighten(rgb, width, height);
            rgb = straightened.Rgb;
            width = straightened.Width;
            height = straightened.Height;
            angle = straightened.AngleDegrees;
            geometryChanged |= Math.Abs(angle) >= ScanDeskew.SkipBelow;
        }

        if (options.AutoTrim)
        {
            var trimmed = ScanRaster.TrimMargins(rgb, width, height);
            geometryChanged |= trimmed.Width != width || trimmed.Height != height;
            rgb = trimmed.Rgb;
            width = trimmed.Width;
            height = trimmed.Height;
        }

        var jpeg = ScanJpegEncoder.EncodeRgb(rgb, width, height, page.ColorMode);
        var keepLogical = !geometryChanged && page.PixelWidth > 0 && page.PixelWidth != width;
        return page with
        {
            ImageBytes = jpeg,
            Format = ScanImageFormat.Jpeg,
            RasterRgb = rgb,
            RasterWidth = width,
            RasterHeight = height,
            PixelWidth = keepLogical ? page.PixelWidth : width,
            PixelHeight = keepLogical ? page.PixelHeight : height,
            DeskewDegrees = angle,
            HiddenAsBlank = false,
            SourceImageBytes = page.SourceImageBytes ?? page.ImageBytes,
            SourcePixelWidth = page.SourcePixelWidth > 0 ? page.SourcePixelWidth : page.PixelWidth,
            SourcePixelHeight = page.SourcePixelHeight > 0 ? page.SourcePixelHeight : page.PixelHeight
        };
    }

    public static ScannedPage Crop(ScannedPage page, ScanCropRect rect)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!ScanRaster.TryGetRgb(page, out var width, out var height, out var rgb))
            return page;

        var cropped = ScanRaster.Crop(rgb, width, height, rect);
        var jpeg = ScanJpegEncoder.EncodeRgb(cropped.Rgb, cropped.Width, cropped.Height, page.ColorMode);
        return page with
        {
            ImageBytes = jpeg,
            Format = ScanImageFormat.Jpeg,
            RasterRgb = cropped.Rgb,
            RasterWidth = cropped.Width,
            RasterHeight = cropped.Height,
            PixelWidth = cropped.Width,
            PixelHeight = cropped.Height,
            SourceImageBytes = page.SourceImageBytes ?? page.ImageBytes,
            SourceRasterPath = page.SourceRasterPath,
            SourcePixelWidth = page.SourcePixelWidth > 0 ? page.SourcePixelWidth : page.PixelWidth,
            SourcePixelHeight = page.SourcePixelHeight > 0 ? page.SourcePixelHeight : page.PixelHeight
        };
    }

    public static ScannedPage Trim(ScannedPage page) =>
        Process(page, new ScanPipelineOptions
        {
            DropBlankPages = false,
            AutoDeskew = false,
            AutoTrim = true
        });
}
