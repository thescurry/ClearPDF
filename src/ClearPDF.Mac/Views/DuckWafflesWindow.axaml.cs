using Avalonia.Controls;
using Avalonia.Input;

namespace ClearPDF.Mac.Views;

public partial class DuckWafflesWindow : Window
{
    public DuckWafflesWindow()
    {
        InitializeComponent();
    }

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e) => Close();

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Enter or Key.Space)
            Close();
    }
}
