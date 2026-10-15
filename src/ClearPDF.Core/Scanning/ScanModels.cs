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
    Unknown = 6
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
}

public sealed record ScanProgress(int PagesCompleted, int? PagesTotal, string? Status);

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

    public ScannedPage RotateBy(int deltaDegrees) =>
        this with { RotationDegrees = ScanPageLayout.NormalizeDegrees(RotationDegrees + deltaDegrees) };

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
