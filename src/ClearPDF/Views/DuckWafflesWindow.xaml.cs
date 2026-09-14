using System.Windows;
using System.Windows.Input;

namespace ClearPDF.Views;

public partial class DuckWafflesWindow : Window
{
    public DuckWafflesWindow()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Enter or Key.Space)
            Close();
    }
}
