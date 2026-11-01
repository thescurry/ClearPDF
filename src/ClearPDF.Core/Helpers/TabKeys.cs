namespace ClearPDF.Helpers;

/// <summary>
/// Document-tab gestures (Cmd+W, Cmd+Shift+[ / ]). Bracket keys vary by
/// Avalonia mapping (<c>OemOpenBrackets</c>, <c>Oem4</c>, or <c>[</c>).
/// </summary>
public enum TabAction
{
    None,
    CloseCurrent,
    Previous,
    Next
}

public static class TabKeys
{
    public static TabAction Resolve(string? key, bool shift, bool command)
    {
        if (string.IsNullOrWhiteSpace(key) || !command)
            return TabAction.None;

        if (IsKey(key, "W"))
            return shift ? TabAction.None : TabAction.CloseCurrent;

        if (shift && IsOpenBracket(key))
            return TabAction.Previous;

        if (shift && IsCloseBracket(key))
            return TabAction.Next;

        return TabAction.None;
    }

    private static bool IsOpenBracket(string key) =>
        IsKey(key, "OemOpenBrackets") || IsKey(key, "Oem4") || IsKey(key, "[");

    private static bool IsCloseBracket(string key) =>
        IsKey(key, "OemCloseBrackets") || IsKey(key, "Oem6") || IsKey(key, "]");

    private static bool IsKey(string key, string name) =>
        string.Equals(key, name, StringComparison.OrdinalIgnoreCase);
}
