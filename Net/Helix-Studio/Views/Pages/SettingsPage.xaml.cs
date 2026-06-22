using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Helix_Studio.ViewModels;

namespace Helix_Studio.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SettingsViewModel oldVm)
            oldVm.PropertyChanged -= OnSettingsVmPropertyChanged;
        if (e.NewValue is SettingsViewModel newVm)
        {
            newVm.PropertyChanged += OnSettingsVmPropertyChanged;
            if (newVm.ActiveSection.Length > 0)
                Dispatcher.BeginInvoke(() => JumpToSection(newVm.ActiveSection), DispatcherPriority.Loaded);
        }
    }

    private void OnSettingsVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ActiveSection) && sender is SettingsViewModel vm)
            JumpToSection(vm.ActiveSection);
    }

    private void JumpToSection(string section)
    {
        var btn = section switch
        {
            "General"    => (RadioButton?)NavGeneral,
            "Display"    => NavDisplay,
            "Workspaces" => NavWorkspaces,
            "Log"        => NavLog,
            "Alerts"     => NavAlerts,
            "About"      => NavAbout,
            _            => null,
        };
        if (btn is null) return;
        btn.IsChecked = true;
        if (DataContext is SettingsViewModel svm && svm.ActiveSection == section)
            svm.ActiveSection = string.Empty;
    }

    private void SettingsNav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;
        var target = tag switch
        {
            "General"    => (FrameworkElement?)SectionGeneral,
            "Display"    => SectionDisplay,
            "Workspaces" => SectionWorkspaces,
            "Log"        => SectionLog,
            "Alerts"     => SectionAlerts,
            "About"      => SectionAbout,
            _            => null,
        };
        target?.BringIntoView();
    }
}
