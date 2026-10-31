namespace ClearPDF.Scanning;

public enum WiaTransferAction
{
    RetryFormat = 0,
    EndOfFeeder = 1,
    Fail = 2
}

public readonly record struct WiaTransferDecision(WiaTransferAction Action, ScannerErrorKind? Kind);

/// <summary>
/// WIA transfer retry / abort rules, kept in Core so Linux tests lock them
/// without COM. Format errors may try another image format. Jam, busy,
/// cover-open, disconnect, and misfeed abort the job and never pull another
/// sheet.
/// </summary>
public static class WiaTransferPolicy
{
    public const int ENotImpl = unchecked((int)0x80004001);
    public const int InvalidData = unchecked((int)0x8007000D);

    public static bool IsFormatRetryable(int hr) =>
        hr == ScannerErrorMapper.WiaInvalidCommand ||
        hr == ENotImpl ||
        hr == InvalidData;

    public static bool ShouldAbortJob(ScannerErrorKind kind)
    {
        switch (kind)
        {
            case ScannerErrorKind.PaperJam:
            case ScannerErrorKind.DeviceBusy:
            case ScannerErrorKind.CoverOpen:
            case ScannerErrorKind.Communication:
            case ScannerErrorKind.DeviceNotFound:
            case ScannerErrorKind.Misfeed:
                return true;
            case ScannerErrorKind.FeederEmpty:
            case ScannerErrorKind.NoPaper:
            case ScannerErrorKind.Cancelled:
            case ScannerErrorKind.Unknown:
                return false;
            default:
            {
                ScannerErrorKind unreachable = kind;
                throw new ArgumentOutOfRangeException(nameof(kind), unreachable, null);
            }
        }
    }

    public static WiaTransferDecision Decide(int hr, ScanSource source, int pagesCompleted)
    {
        if (ScannerErrorMapper.IsEndOfFeeder(hr))
        {
            if (pagesCompleted > 0)
                return new WiaTransferDecision(WiaTransferAction.EndOfFeeder, null);
            return new WiaTransferDecision(
                WiaTransferAction.Fail,
                source == ScanSource.Feeder ? ScannerErrorKind.FeederEmpty : ScannerErrorKind.NoPaper);
        }

        if (IsFormatRetryable(hr))
            return new WiaTransferDecision(WiaTransferAction.RetryFormat, null);

        var kind = ScannerErrorMapper.FromHResult(hr, source);
        return new WiaTransferDecision(WiaTransferAction.Fail, kind);
    }
}
