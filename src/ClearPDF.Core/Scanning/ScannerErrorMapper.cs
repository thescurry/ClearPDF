namespace ClearPDF.Scanning;

/// <summary>
/// HRESULT mapping for WIA 2.0 (wiaerror.h). Kept in Core so Linux tests
/// lock the feeder-empty / busy / cancel behavior without COM.
/// </summary>
public static class ScannerErrorMapper
{
    public const int WiaPaperJam = unchecked((int)0x80210002);
    public const int WiaPaperEmpty = unchecked((int)0x80210003);
    public const int WiaPaperProblem = unchecked((int)0x80210004);
    public const int WiaOffline = unchecked((int)0x80210005);
    public const int WiaBusy = unchecked((int)0x80210006);
    public const int WiaUserIntervention = unchecked((int)0x80210008);
    public const int WiaDeviceCommunication = unchecked((int)0x8021000A);
    public const int WiaDeviceLocked = unchecked((int)0x8021000D);
    public const int WiaCoverOpen = unchecked((int)0x80210016);
    public const int WiaNoDevice = unchecked((int)0x80210015);
    public const int CancelledHResult = unchecked((int)0x800704C7); // ERROR_CANCELLED
    public const int RpcCallFailed = unchecked((int)0x800706BE);

    public static bool IsEndOfFeeder(int hr) =>
        hr == WiaPaperEmpty || hr == WiaPaperProblem;

    public static ScannerErrorKind FromHResult(int hr, ScanSource source)
    {
        if (hr == CancelledHResult)
            return ScannerErrorKind.Cancelled;
        if (IsEndOfFeeder(hr))
            return source == ScanSource.Feeder
                ? ScannerErrorKind.FeederEmpty
                : ScannerErrorKind.NoPaper;
        if (hr == WiaBusy || hr == WiaDeviceLocked)
            return ScannerErrorKind.DeviceBusy;
        if (hr == WiaNoDevice)
            return ScannerErrorKind.DeviceNotFound;
        if (hr == WiaOffline || hr == WiaDeviceCommunication || hr == RpcCallFailed ||
            hr == WiaCoverOpen || hr == WiaUserIntervention || hr == WiaPaperJam)
            return ScannerErrorKind.Communication;

        return ScannerErrorKind.Unknown;
    }
}
