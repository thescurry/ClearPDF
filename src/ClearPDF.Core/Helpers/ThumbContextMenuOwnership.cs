namespace ClearPDF.Helpers;

/// <summary>
/// WPF <c>ContextMenu</c> may have only one logical parent. Sharing one
/// instance across N thumb rows throws <c>InvalidOperationException</c>
/// ("already the logical child of another element") when the menu is
/// opened from a second row. Each thumb must own a distinct menu.
/// </summary>
public static class ThumbContextMenuOwnership
{
    /// <summary>
    /// Never share a single menu across the rail. A cached singleton
    /// assigned to every row is the v1.2 crash.
    /// </summary>
    public static bool CanShareOneInstanceAcrossThumbs => false;

    /// <summary>
    /// Distinct menu instances required for <paramref name="thumbCount"/> rows.
    /// One per row — never 1-for-N.
    /// </summary>
    public static int InstanceCountForThumbs(int thumbCount) =>
        thumbCount < 0 ? 0 : thumbCount;

    /// <summary>
    /// Stand-in for WPF logical-parent attach. Returns <c>false</c> when
    /// <paramref name="child"/> already belongs to a different parent
    /// (the crash if a shared <c>ContextMenu</c> is opened from another thumb).
    /// </summary>
    public static bool TryClaimParent(
        IDictionary<object, object> childToParent,
        object child,
        object parent)
    {
        ArgumentNullException.ThrowIfNull(childToParent);
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(parent);

        if (childToParent.TryGetValue(child, out var existing) &&
            !ReferenceEquals(existing, parent))
            return false;

        childToParent[child] = parent;
        return true;
    }
}
