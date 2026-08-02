using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Vaeron.HelixStudio.Internal.Runtime.Components;

namespace Vaeron.HelixStudio;

public partial class ToolHostView : UserControl
{
    private readonly ToolHostViewModel _viewModel;
    private nint _embeddedHandle;

    public ToolHostView(ToolHostViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Embed();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolHostViewModel.WindowHandle)) Embed();
    }

    private void Embed()
    {
        var hwnd = _viewModel.WindowHandle;
        if (hwnd == _embeddedHandle) return; // 已是同一視窗（或同為 0），不重做
        _embeddedHandle = hwnd;

        // 延到版面就緒後再嵌入，確保宿主已實體化，SetParent 才會成功
        Dispatcher.BeginInvoke(new Action(() =>
        {
            HostArea.Child = hwnd != nint.Zero ? new EmbeddedWindowHost(hwnd) : null;
        }), DispatcherPriority.Loaded);
    }
}
