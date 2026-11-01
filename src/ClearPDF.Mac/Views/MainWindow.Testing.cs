using Avalonia.Input;
using ClearPDF.Services;

namespace ClearPDF.Mac.Views;

public partial class MainWindow
{
    internal void OpenDocumentForTests(IPdfDocument doc, string path) =>
        AttachNewDocument(doc, path);

    internal void DriveWindowKeyDown(KeyEventArgs e) =>
        Window_KeyDown(this, e);

    internal void DriveFindBoxKey(KeyEventArgs e, bool fromKeyUp) =>
        HandleFindBoxKey(e, fromKeyUp);

    internal void BlurFindBoxForTests()
    {
        _suppressFindKeyUp = false;
        PageScroller.Focus();
    }

    internal bool FindIsOpen => Find?.IsOpen == true;
    internal int FindIndex => Find?.CurrentIndex ?? -1;
    internal string FindStatusText => Find?.StatusText ?? string.Empty;
    internal int TabCount => _tabs.Count;
    internal string? CurrentTabTitle => _tabs.Current?.Title;
    internal bool SuppressFindKeyUp => _suppressFindKeyUp;

    internal static KeyEventArgs NewKey(
        Key key,
        KeyModifiers modifiers = KeyModifiers.None,
        PhysicalKey? physical = null,
        bool keyUp = false)
    {
        return new KeyEventArgs
        {
            RoutedEvent = keyUp ? InputElement.KeyUpEvent : InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
            PhysicalKey = physical ?? PhysicalKey.None
        };
    }
}
