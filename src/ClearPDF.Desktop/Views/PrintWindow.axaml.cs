using Avalonia.Controls;
using Avalonia.Interactivity;
using ClearPDF.Helpers;

namespace ClearPDF.Desktop.Views;

public partial class PrintWindow : Window
{
    private readonly int _pageCount;
    private readonly int _currentPage0;

    public PrintRangeMode? ChosenMode { get; private set; }
    public int From1Based { get; private set; } = 1;
    public int To1Based { get; private set; } = 1;

    public PrintWindow() : this(1, 0) { }

    public PrintWindow(int pageCount, int currentPage0)
    {
        _pageCount = Math.Max(1, pageCount);
        _currentPage0 = currentPage0;
        InitializeComponent();
        SummaryText.Text = $"Print {_pageCount} page{(_pageCount == 1 ? "" : "s")} via the system printer (lp).";
        var cur = Math.Clamp(currentPage0, 0, _pageCount - 1) + 1;
        FromBox.Text = "1";
        ToBox.Text = _pageCount.ToString();
        CurrentRadio.Content = $"Current page ({cur})";
    }

    private void Print_Click(object? sender, RoutedEventArgs e)
    {
        if (CurrentRadio.IsChecked == true)
        {
            ChosenMode = PrintRangeMode.Selection;
            From1Based = To1Based = _currentPage0 + 1;
        }
        else if (RangeRadio.IsChecked == true)
        {
            ChosenMode = PrintRangeMode.UserPages;
            From1Based = Parse(FromBox.Text, 1);
            To1Based = Parse(ToBox.Text, _pageCount);
        }
        else
        {
            ChosenMode = PrintRangeMode.AllPages;
            From1Based = 1;
            To1Based = _pageCount;
        }

        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        ChosenMode = null;
        Close(false);
    }

    private static int Parse(string? text, int fallback) =>
        int.TryParse(text, out var n) ? n : fallback;
}
