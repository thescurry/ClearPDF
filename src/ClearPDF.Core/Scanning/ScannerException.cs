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
                return "The scanner is not available.";
            case ScannerErrorKind.Communication:
                return "Could not talk to the scanner.";
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
