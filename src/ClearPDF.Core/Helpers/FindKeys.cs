namespace ClearPDF.Helpers;

/// <summary>
/// Find box / window gestures. Avalonia on macOS reports the Return key as
/// <c>Return</c>, not <c>Enter</c> — both must commit the search.
/// </summary>
public enum FindAction
{
    None,
    Toggle,
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
            return FindAction.Toggle;

        if (IsKey(key, "Escape") || IsKey(key, "Esc"))
            return FindAction.Close;

        return FindAction.None;
    }

    public static bool IsEnter(string? key) =>
        IsKey(key, "Enter") || IsKey(key, "Return");

    private static bool IsKey(string? key, string name) =>
        string.Equals(key, name, StringComparison.OrdinalIgnoreCase);
}
