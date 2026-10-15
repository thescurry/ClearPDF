using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ClearPDF.Desktop.Views;

public partial class PasswordWindow : Window
{
    public string? Password { get; private set; }

    public PasswordWindow()
    {
        InitializeComponent();
        PasswordBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Accept();
                e.Handled = true;
            }
        };
    }

    private void Unlock_Click(object? sender, RoutedEventArgs e) => Accept();

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Password = null;
        Close(false);
    }

    private void Accept()
    {
        // PdfUnlockPolicy.PromptExists is the product contract; view unlock only.
        Password = PasswordBox.Text;
        Close(true);
    }
}
