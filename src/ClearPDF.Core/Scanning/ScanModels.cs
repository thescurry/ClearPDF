namespace ClearPDF.Scanning;

public enum ScanSource
{
    Flatbed = 0,
    Feeder = 1
}

public enum ScanColorMode
{
    Color = 0,
    Grayscale = 1,
    BlackAndWhite = 2
}

public enum ScanImageFormat
{
    Jpeg = 0,
    Bmp = 1,
    Png = 2
}

public enum ScannerErrorKind
{
    FeederEmpty = 0,
    NoPaper = 1,
    DeviceBusy = 2,
    Cancelled = 3,
    DeviceNotFound = 4,
    Communication = 5,
    Unknown = 6,
    PaperJam = 7,
    CoverOpen = 8,
    Misfeed = 9
}

public enum ScanInsertMode
{
    Start = 0,
    AfterPage = 1,
    End = 2
}

public sealed record ScanDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool SupportsFlatbed { get; init; } = true;
    public bool SupportsFeeder { get; init; }
    public bool SupportsDuplex { get; init; }
    public IReadOnlyList<int> DpiOptions { get; init; } = ScanDefaults.DpiOptions;
    public IReadOnlyList<ScanColorMode> ColorModes { get; init; } = ScanDefaults.ColorModes;
}

public sealed record ScanSettings
{
    public ScanSource Source { get; init; } = ScanSource.Flatbed;
    public ScanColorMode ColorMode { get; init; } = ScanColorMode.Color;
    public int Dpi { get; init; } = 300;
    public bool Duplex { get; init; }

    /// <summary>Stop after this many sheets. 1 = one-click / rescan / flatbed step.</summary>
    public int? MaxPages { get; init; }
}

public sealed record ScanProgress(
    int PagesCompleted,
    int? PagesTotal,
    string? Status,
    ScannedPage? Page = null);

/// <summary>Normalized crop in 0..1 page coordinates (after the current 90° rotation).</summary>
public readonly record struct ScanCropRect(double Left, double Top, double Right, double Bottom)
{
    public static ScanCropRect Full => new(0, 0, 1, 1);

    public ScanCropRect Clamp()
    {
        var left = Math.Clamp(Math.Min(Left, Right), 0, 1);
        var top = Math.Clamp(Math.Min(Top, Bottom), 0, 1);
        var right = Math.Clamp(Math.Max(Left, Right), 0, 1);
        var bottom = Math.Clamp(Math.Max(Top, Bottom), 0, 1);
        if (right - left < 0.02)
            right = Math.Min(1, left + 0.02);
        if (bottom - top < 0.02)
            bottom = Math.Min(1, top + 0.02);
        return new ScanCropRect(left, top, right, bottom);
    }
}

public sealed record ScanPipelineOptions
{
    public bool DropBlankPages { get; init; } = true;
    public bool AutoDeskew { get; init; } = true;
    public bool AutoTrim { get; init; } = true;
}

/// <summary>
/// One scanned sheet. <see cref="PixelWidth"/> / <see cref="PixelHeight"/> are
/// the page's physical pixel size (used for PDF page sizing) and may be larger
/// than the JPEG pixel grid when a mock/thumbnail image is stretched.
/// </summary>
public sealed record ScannedPage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required byte[] ImageBytes { get; init; }
    public ScanImageFormat Format { get; init; } = ScanImageFormat.Jpeg;
    public required int PixelWidth { get; init; }
    public required int PixelHeight { get; init; }
    public required int Dpi { get; init; }
    public ScanColorMode ColorMode { get; init; } = ScanColorMode.Color;
    public int RotationDegrees { get; init; }

    /// <summary>Optional decoded RGB (R,G,B packed). Used by deskew / blank / crop.</summary>
    public byte[]? RasterRgb { get; init; }
    public int RasterWidth { get; init; }
    public int RasterHeight { get; init; }
    public double DeskewDegrees { get; init; }

    /// <summary>Small JPEG for the strip. Full raster lives on <see cref="RasterPath"/>.</summary>
    public byte[]? ThumbnailBytes { get; init; }
    public string? RasterPath { get; init; }
    public bool HiddenAsBlank { get; init; }

    public byte[]? SourceImageBytes { get; init; }
    public string? SourceRasterPath { get; init; }
    public int SourcePixelWidth { get; init; }
    public int SourcePixelHeight { get; init; }

    public ScannedPage RotateBy(int deltaDegrees) =>
        this with { RotationDegrees = ScanPageLayout.NormalizeDegrees(RotationDegrees + deltaDegrees) };

    public ScannedPage RestoreHidden() => this with { HiddenAsBlank = false };

    public ScannedPage RevertToSource() =>
        this with
        {
            ImageBytes = SourceImageBytes ?? ImageBytes,
            RasterPath = SourceRasterPath ?? RasterPath,
            RasterRgb = null,
            PixelWidth = SourcePixelWidth > 0 ? SourcePixelWidth : PixelWidth,
            PixelHeight = SourcePixelHeight > 0 ? SourcePixelHeight : PixelHeight,
            RasterWidth = SourcePixelWidth > 0 ? SourcePixelWidth : RasterWidth,
            RasterHeight = SourcePixelHeight > 0 ? SourcePixelHeight : RasterHeight,
            RotationDegrees = 0,
            DeskewDegrees = 0,
            HiddenAsBlank = false
        };

    public (int Width, int Height) OrientedPixels =>
        ScanPageLayout.IsQuarterTurn(RotationDegrees)
            ? (PixelHeight, PixelWidth)
            : (PixelWidth, PixelHeight);
}

public static class ScanDefaults
{
    public static readonly int[] DpiOptions = { 150, 200, 300 };

    public static readonly ScanColorMode[] ColorModes =
    {
        ScanColorMode.Color,
        ScanColorMode.Grayscale,
        ScanColorMode.BlackAndWhite
    };
}
