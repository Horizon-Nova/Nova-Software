using System;
using System.Windows;
using System.Windows.Controls;
using Helix_Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Helix_Studio.Views;

/// <summary>
/// 概覽頁面的檢視層。
/// </summary>
public partial class OverviewPage : UserControl
{
    /// <summary>
    /// 建立概覽頁面並接上 ViewModel。
    /// </summary>
    public OverviewPage()
    {
        InitializeComponent();

        var provider = ((App)Application.Current).QueryServiceProvider();
        var viewModel = provider.GetRequiredService<OverviewPageViewModel>();
        DataContext = viewModel;
        viewModel.ApplyViewLoaded();
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        Unloaded -= OnUnloaded;
    }

    private void GoToSettings_Click(object sender, RoutedEventArgs e)
        => ApplyNavigation("Settings");

    private void GoToWorkspaces_Click(object sender, RoutedEventArgs e)
        => ApplyNavigation("Settings");

    private void GoToModules_Click(object sender, RoutedEventArgs e)
        => ApplyNavigation("Modules");

    private void GoToEventLog_Click(object sender, RoutedEventArgs e)
        => ApplyNavigation("EventLog");

    private void OpenModule_Click(object sender, RoutedEventArgs e)
        => ApplyNavigation("Modules");

    private void ApplyNavigation(string tag)
    {
        if (Window.GetWindow(this) is not MainWindow mainWindow)
        {
            return;
        }

        var radio = mainWindow.FindName($"Nav{tag}") as RadioButton;
        if (radio is not null)
        {
            radio.IsChecked = true;
        }
    }
}
