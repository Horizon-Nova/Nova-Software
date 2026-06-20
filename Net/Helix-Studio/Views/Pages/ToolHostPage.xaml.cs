using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;
using Helix_Studio.ViewModels;
using Helix_Studio.Views.Controls;

namespace Helix_Studio.Views.Pages;

public partial class ToolHostPage : UserControl
{
    private ToolHostViewModel? _vm;
    private nint _embeddedHandle;   // 目前已嵌入的視窗，避免重複 reparent

    public ToolHostPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => Embed();
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = e.NewValue as ToolHostViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
        Embed();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolHostViewModel.WindowHandle)) Embed();
    }

    private void Embed()
    {
        if (_vm is null) return;
        var hwnd = _vm.WindowHandle;
        if (hwnd == _embeddedHandle) return;   // 已是同一視窗（或同為 0），不重做
        _embeddedHandle = hwnd;

        // 延到版面就緒後再嵌入，確保宿主已實體化，SetParent 才會成功
        Dispatcher.BeginInvoke(new Action(() =>
        {
            HostArea.Child = hwnd != nint.Zero ? new EmbeddedWindowHost(hwnd) : null;
        }), DispatcherPriority.Loaded);
    }
}
