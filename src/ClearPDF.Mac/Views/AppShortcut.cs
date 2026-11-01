namespace ClearPDF.Mac.Views;

/// <summary>
/// Menu-owned commands. Each value has exactly one apply path
/// (<see cref="MainWindow.ExecuteShortcut"/>).
/// </summary>
internal enum AppShortcut
{
    Open,
    FindOpen,
    FindNext,
    FindPrevious,
    CloseTab,
    PreviousTab,
    NextTab
}
