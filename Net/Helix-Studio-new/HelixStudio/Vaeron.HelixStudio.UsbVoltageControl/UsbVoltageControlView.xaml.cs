using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.UsbVoltageControl;

public partial class UsbVoltageControlView : UserControl
{
    public UsbVoltageControlView(UsbVoltageControlViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => ((UsbVoltageControlViewModel)DataContext).ApplyViewLoaded();

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => ((UsbVoltageControlViewModel)DataContext).ApplyViewUnloaded();
}
