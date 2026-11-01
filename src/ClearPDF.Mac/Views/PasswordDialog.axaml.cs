using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClearPDF.Helpers;

namespace ClearPDF.Mac.Views;

public partial class PasswordDialog : Window
{
    public string? Password { get; private set; }

    public PasswordDialog()
    {
        InitializeComponent();
        Opened += (_, _) => PasswordBox.Focus();
    }

    public void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
        PasswordBox.Text = string.Empty;
        PasswordBox.Focus();
    }

    private void Unlock_Click(object? sender, RoutedEventArgs e) => Accept();

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Password = null;
        Close(false);
    }

    private void PasswordBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (FindKeys.IsEnter(e.Key.ToString()))
            Accept();
        else if (e.Key == Key.Escape)
            Close(false);
    }

    private void Accept()
    {
        Password = PasswordBox.Text;
        if (string.IsNullOrEmpty(Password))
        {
            ShowError("Enter a password to unlock this PDF.");
            return;
        }

        Close(true);
    }
}
