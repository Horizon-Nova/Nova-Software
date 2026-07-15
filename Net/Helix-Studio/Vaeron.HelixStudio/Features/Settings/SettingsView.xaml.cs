using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.Features.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var vm = (SettingsViewModel)DataContext;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.ApplyViewLoaded();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var vm = (SettingsViewModel)DataContext;
        vm.PropertyChanged -= OnViewModelPropertyChanged;
        vm.ApplyViewUnloaded();
    }

    /// <summary>左側分類導覽點擊後捲動到對應區塊——比對 blueprint 的錨點捲動行為。
    /// 用具名 Border（Section_{key}）＋ FindName 查找，不另外引入導覽/路由機制。</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.SelectedCategoryKey)) return;
        var key = ((SettingsViewModel)DataContext).SelectedCategoryKey;
        if (key is null) return;
        if (FindName($"Section_{key}") is FrameworkElement section)
            section.BringIntoView();
    }
}
