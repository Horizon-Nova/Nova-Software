using System;
using System.Collections.Generic;
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
    private static readonly IReadOnlyDictionary<string, string> QuickNavPageIds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Settings"] = "settings",
            ["Modules"] = "modules",
            ["EventLog"] = "eventlog",
        };

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
        if (!QuickNavPageIds.TryGetValue(tag, out var pageId))
        {
            return;
        }

        if (Window.GetWindow(this)?.DataContext is not MainWindowWindowViewModel shellViewModel)
        {
            return;
        }

        shellViewModel.NavigateCommand.Execute(pageId);
    }
}
