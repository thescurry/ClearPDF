namespace ClearPDF.Scanning;

public interface IScanner
{
    ScanDeviceInfo Device { get; }

    /// <summary>
    /// Run a scan. Feeder jobs may return several pages. Cancel is checked
    /// between pages; a mid-page WIA transfer cannot always abort instantly.
    /// </summary>
    Task<IReadOnlyList<ScannedPage>> ScanAsync(
        ScanSettings settings,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken);
}

public interface IScannerProvider
{
    IReadOnlyList<ScanDeviceInfo> ListDevices();
    IScanner Open(string deviceId);
}
