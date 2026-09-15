namespace ClearPDF.Helpers;

/// <summary>
/// View-unlock contract for encrypted PDFs. The WPF shell shows
/// <c>PasswordDialog</c>; this type is the Linux-testable policy so we do not
/// invent storage, DRM, or a new installer.
/// </summary>
public static class PdfUnlockPolicy
{
    /// <summary>A password prompt exists for encrypted documents.</summary>
    public const bool PromptExists = true;

    /// <summary>Unlock is for viewing only — not form fill, edit, or DRM.</summary>
    public const bool ViewUnlockOnly = true;

    /// <summary>Purpose string accepted by <see cref="IsViewUnlockPurpose"/>.</summary>
    public const string ViewPurpose = "view";

    /// <summary>Never write the password to disk, recents, or logs.</summary>
    public static bool ShouldPersistPassword => false;

    /// <summary>Wrong password → prompt again; Cancel aborts open.</summary>
    public static bool ShouldPromptAgainAfterFailure => true;

    public static bool ShouldPrompt(bool requiresPassword, string? suppliedPassword) =>
        requiresPassword && string.IsNullOrEmpty(suppliedPassword);

    public static bool IsViewUnlockPurpose(string? purpose) =>
        string.Equals(purpose, ViewPurpose, StringComparison.OrdinalIgnoreCase);
}
