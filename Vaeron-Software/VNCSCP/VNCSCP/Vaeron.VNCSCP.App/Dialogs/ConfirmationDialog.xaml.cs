using System.Windows;

namespace Vaeron.VNCSCP.App;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void OnYesClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnNoClick(object sender, RoutedEventArgs e) => DialogResult = false;
}

