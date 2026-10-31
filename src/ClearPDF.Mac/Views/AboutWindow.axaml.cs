using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ClearPDF.Mac.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        AboutText.Text = ProductInfo.About + "\nA simple local PDF reader for Intel macOS.";
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close();
}
