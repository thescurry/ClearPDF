namespace ClearPDF.Scanning;

public sealed class MockScannerProvider : IScannerProvider
{
    private readonly Dictionary<string, MockScanner> _devices;

    public MockScannerProvider(params MockScanner[] scanners)
    {
        _devices = new Dictionary<string, MockScanner>(StringComparer.OrdinalIgnoreCase);
        foreach (var scanner in scanners)
            _devices[scanner.Device.Id] = scanner;
    }

    public IReadOnlyList<ScanDeviceInfo> ListDevices() =>
        _devices.Values.Select(s => s.Device).ToList();

    public IScanner Open(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || !_devices.TryGetValue(deviceId, out var scanner))
            throw new ScannerException(ScannerErrorKind.DeviceNotFound);
        return scanner;
    }
}

/// <summary>
/// In-memory scanner for tests and for UI work without hardware. Queue the
/// pages <see cref="ScanAsync"/> should return; set <see cref="Error"/> /
/// <see cref="IsBusy"/> to simulate feeder-empty, busy, and cancel.
/// </summary>
public sealed class MockScanner : IScanner
{
    public MockScanner(ScanDeviceInfo device)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
    }

    public ScanDeviceInfo Device { get; }

    public List<ScannedPage> Queue { get; } = new();

    public ScannerErrorKind? Error { get; set; }

    /// <summary>Fail with <see cref="Error"/> after this many successful pages (0 = before any).</summary>
    public int ErrorAfterPages { get; set; }

    public bool IsBusy { get; set; }

    public int ScanCallCount { get; private set; }

    public List<ScanProgress> ProgressReports { get; } = new();

    public static MockScanner HpEnvy7640() => new(new ScanDeviceInfo
    {
        Id = "mock-hp-envy-7640",
        Name = "HP ENVY 7640",
        SupportsFlatbed = true,
        SupportsFeeder = false,
        SupportsDuplex = false,
        DpiOptions = ScanDefaults.DpiOptions,
        ColorModes = ScanDefaults.ColorModes
    });

    public static MockScanner HpColorLaserJetM277dw() => new(new ScanDeviceInfo
    {
        Id = "mock-hp-m277dw",
        Name = "HP Color LaserJet MFP M277dw",
        SupportsFlatbed = true,
        SupportsFeeder = true,
        SupportsDuplex = true,
        DpiOptions = ScanDefaults.DpiOptions,
        ColorModes = ScanDefaults.ColorModes
    });

    /// <summary>
    /// Solid-color JPEG. PixelWidth/Height are the logical scan size (PDF page
    /// sizing); the JPEG itself is a small swatch so tests stay cheap.
    /// </summary>
    public static ScannedPage CreateSolidPage(
        int width,
        int height,
        int dpi,
        ScanColorMode colorMode,
        byte r,
        byte g,
        byte b)
    {
        const int swatch = 16;
        var rgb = new byte[swatch * swatch * 3];
        for (var i = 0; i < rgb.Length; i += 3)
        {
            rgb[i] = r;
            rgb[i + 1] = g;
            rgb[i + 2] = b;
        }

        var jpeg = ScanJpegEncoder.EncodeRgb(rgb, swatch, swatch, colorMode);
        return new ScannedPage
        {
            ImageBytes = jpeg,
            Format = ScanImageFormat.Jpeg,
            PixelWidth = Math.Max(1, width),
            PixelHeight = Math.Max(1, height),
            Dpi = Math.Max(1, dpi),
            ColorMode = colorMode
        };
    }

    public async Task<IReadOnlyList<ScannedPage>> ScanAsync(
        ScanSettings settings,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ScanCallCount++;
        cancellationToken.ThrowIfCancellationRequested();

        if (IsBusy)
            throw new ScannerException(ScannerErrorKind.DeviceBusy);

        if (settings.Source == ScanSource.Feeder && !Device.SupportsFeeder)
            throw new ScannerException(ScannerErrorKind.Unknown, "This scanner has no document feeder.");
        if (settings.Source == ScanSource.Flatbed && !Device.SupportsFlatbed)
            throw new ScannerException(ScannerErrorKind.Unknown, "This scanner has no flatbed.");
        if (settings.Duplex && !Device.SupportsDuplex)
            throw new ScannerException(ScannerErrorKind.Unknown, "This scanner cannot duplex.");

        if (Error is { } fail && ErrorAfterPages <= 0)
            throw new ScannerException(fail);

        var pages = new List<ScannedPage>();
        var total = Queue.Count;
        for (var i = 0; i < Queue.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Error is { } kind && i >= ErrorAfterPages && ErrorAfterPages > 0)
                throw new ScannerException(kind);

            pages.Add(ClonePage(Queue[i], settings));
            var report = new ScanProgress(pages.Count, total, $"Page {pages.Count}");
            ProgressReports.Add(report);
            progress?.Report(report);
            await Task.Yield();
        }

        if (pages.Count == 0)
        {
            throw new ScannerException(settings.Source == ScanSource.Feeder
                ? ScannerErrorKind.FeederEmpty
                : ScannerErrorKind.NoPaper);
        }

        return pages;
    }

    private static ScannedPage ClonePage(ScannedPage source, ScanSettings settings) =>
        source with
        {
            Dpi = settings.Dpi > 0 ? settings.Dpi : source.Dpi,
            ColorMode = settings.ColorMode,
            ImageBytes = (byte[])source.ImageBytes.Clone()
        };
}
