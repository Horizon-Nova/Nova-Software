using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.Settings;

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

    /// <summary>分類導覽點擊後捲動到對應區塊</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.SelectedCategoryKey)) return;
        var key = ((SettingsViewModel)DataContext).SelectedCategoryKey;
        if (key is null) return;
        if (FindName($"Section_{key}") is FrameworkElement section)
            section.BringIntoView();
    }
}
