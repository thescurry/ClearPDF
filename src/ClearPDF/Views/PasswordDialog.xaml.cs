using System.Windows;
using System.Windows.Input;

namespace ClearPDF.Views;

public partial class PasswordDialog : Window
{
    public string? Password { get; private set; }

    public PasswordDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => PasswordBox.Focus();
    }

    public void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        PasswordBox.Clear();
        PasswordBox.Focus();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e) => Accept();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Password = null;
        DialogResult = false;
        Close();
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Accept();
    }

    private void Accept()
    {
        // View unlock only — password is not stored beyond this dialog result.
        Password = PasswordBox.Password;
        if (string.IsNullOrEmpty(Password))
        {
            ShowError("Enter a password to unlock this PDF.");
            return;
        }

        DialogResult = true;
        Close();
    }
}
