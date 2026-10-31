namespace ClearPDF.Scanning;

public sealed class ScannerException : Exception
{
    public ScannerErrorKind Kind { get; }

    public ScannerException(ScannerErrorKind kind, string? message = null, Exception? inner = null)
        : base(message ?? MessageFor(kind), inner)
    {
        Kind = kind;
    }

    public static string MessageFor(ScannerErrorKind kind)
    {
        switch (kind)
        {
            case ScannerErrorKind.FeederEmpty:
                return "The document feeder is empty.";
            case ScannerErrorKind.NoPaper:
                return "No paper was found. Place a page on the glass and try again.";
            case ScannerErrorKind.DeviceBusy:
                return "The scanner is busy. Wait a moment and try again.";
            case ScannerErrorKind.Cancelled:
                return "Scan was cancelled.";
            case ScannerErrorKind.DeviceNotFound:
                return "No scanner found. Plug it in, switch it on, and try again.";
            case ScannerErrorKind.Communication:
                return "Could not talk to the scanner. Check the cable and that no other app is using it.";
            case ScannerErrorKind.PaperJam:
                return "Paper jam. Clear the feeder and try again.";
            case ScannerErrorKind.CoverOpen:
                return "The scanner cover is open. Close it and try again.";
            case ScannerErrorKind.Misfeed:
                return "Paper misfeed. Straighten the stack and try again.";
            case ScannerErrorKind.Unknown:
                return "Scan failed.";
            default:
            {
                ScannerErrorKind unreachable = kind;
                throw new ArgumentOutOfRangeException(nameof(kind), unreachable, null);
            }
        }
    }
}
