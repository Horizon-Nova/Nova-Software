using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.RemoteBatchTransfer;

public partial class RemoteBatchTransferView : UserControl
{
    public RemoteBatchTransferView(RemoteBatchTransferViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => ((RemoteBatchTransferViewModel)DataContext).ApplyViewLoaded();

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => ((RemoteBatchTransferViewModel)DataContext).ApplyViewUnloaded();
}
