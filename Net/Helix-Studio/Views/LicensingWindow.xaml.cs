using System.Windows;
using Helix_Studio.ViewModels;

namespace Helix_Studio.Views;

public partial class LicensingWindow : Window
{
    public LicensingWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => KeyBox.Focus();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is LicensingViewModel vm)
                vm.LicenseActivated += OnLicenseActivated;
        };
    }

    private void KeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LicensingViewModel vm)
            vm.KeyInput = KeyBox.Password;
    }

    private void OnLicenseActivated()
    {
        DialogResult = true;
        Close();
    }
}
