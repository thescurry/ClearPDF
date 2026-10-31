namespace ClearPDF.Scanning;

/// <summary>
/// Per-page tidy: optional deskew, margin trim, blank drop, then JPEG for PDF.
/// Scanner I/O stays outside this type so tests can feed fake rasters.
/// </summary>
public static class ScanPagePipeline
{
    public static ScannedPage? Process(ScannedPage page, ScanPipelineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        options ??= new ScanPipelineOptions();

        if (!ScanRaster.TryGetRgb(page, out var width, out var height, out var rgb))
            return page;

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

        if (options.DropBlankPages && ScanBlankDetector.IsBlank(rgb, width, height))
            return null;

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
            DeskewDegrees = angle
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
            PixelHeight = cropped.Height
        };
    }

    public static ScannedPage Trim(ScannedPage page) =>
        Process(page, new ScanPipelineOptions
        {
            DropBlankPages = false,
            AutoDeskew = false,
            AutoTrim = true
        }) ?? page;
}
