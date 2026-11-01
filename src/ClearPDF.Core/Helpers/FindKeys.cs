namespace ClearPDF.Helpers;

/// <summary>
/// Find box / window gestures. Avalonia maps Return and Enter to the same
/// <c>Key</c> value; keypad Enter is a distinct <c>PhysicalKey</c>.
/// ⌘F opens (does not toggle) so a second delivery cannot close the box.
/// </summary>
public enum FindAction
{
    None,
    Toggle,
    Open,
    CommitNext,
    Next,
    Previous,
    Close
}

public static class FindKeys
{
    public static FindAction Resolve(string? key, bool shift, bool command)
    {
        if (string.IsNullOrWhiteSpace(key))
            return FindAction.None;

        if (IsEnter(key))
        {
            if (command)
                return FindAction.None;
            return shift ? FindAction.Previous : FindAction.CommitNext;
        }

        if (IsKey(key, "G") && command)
            return shift ? FindAction.Previous : FindAction.Next;

        if (IsKey(key, "F") && command)
            return FindAction.Open;

        if (IsKey(key, "Escape") || IsKey(key, "Esc"))
            return FindAction.Close;

        return FindAction.None;
    }

    public static bool IsMenuOwned(FindAction action) =>
        action is FindAction.Open or FindAction.Next or FindAction.Previous;

    public static bool IsEnter(string? key) =>
        IsKey(key, "Enter") || IsKey(key, "Return") || IsKey(key, "NumPadEnter");

    private static bool IsKey(string? key, string name) =>
        string.Equals(key, name, StringComparison.OrdinalIgnoreCase);
}
