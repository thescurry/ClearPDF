namespace ClearPDF.Helpers;

/// <summary>
/// Intel Mac MVP command map. Meta is ⌘ (Control on non-Mac shells).
/// Scanner is intentionally absent — out of scope for this MVP.
/// </summary>
public static class MacShortcuts
{
    public enum Command
    {
        Open,
        SaveAs,
        Print,
        Close,
        ZoomIn,
        ZoomOut,
        ActualSize,
        FitWidth,
        FitPage
    }

    public static Command? TryMatch(bool meta, bool shift, bool alt, string? key)
    {
        _ = shift;
        if (!meta || alt || string.IsNullOrWhiteSpace(key))
            return null;

        var k = NormalizeKey(key);
        return k switch
        {
            "O" => Command.Open,
            "S" => Command.SaveAs,
            "P" => Command.Print,
            "W" => Command.Close,
            "+" => Command.ZoomIn,
            "-" => Command.ZoomOut,
            "0" => Command.ActualSize,
            _ => null
        };
    }

    /// <summary>
    /// Avalonia/WPF key names → the single char we match on.
    /// </summary>
    public static string NormalizeKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.Trim().ToUpperInvariant() switch
        {
            "OEMPLUS" or "ADD" or "PLUS" or "=" or "EQUAL" or "OEMEQUALS" => "+",
            "OEMMINUS" or "SUBTRACT" or "MINUS" => "-",
            "D0" or "NUM0" or "NUMPAD0" => "0",
            var s => s.Length == 1 ? s : s
        };
    }

    public static bool IsDefined(Command command)
    {
        switch (command)
        {
            case Command.Open:
            case Command.SaveAs:
            case Command.Print:
            case Command.Close:
            case Command.ZoomIn:
            case Command.ZoomOut:
            case Command.ActualSize:
            case Command.FitWidth:
            case Command.FitPage:
                return true;
            default:
                return false;
        }
    }
}
