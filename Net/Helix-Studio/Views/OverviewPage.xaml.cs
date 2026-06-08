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
    private static readonly IReadOnlyDictionary<string, (string PageId, string? TargetId)> QuickNavigationTargets =
        new Dictionary<string, (string PageId, string? TargetId)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Settings"] = ("settings", null),
            ["Workspaces"] = ("settings", "Workspaces"),
            ["Modules"] = ("modules", null),
            ["EventLog"] = ("eventlog", null),
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
        => ApplyNavigation("Workspaces");

    private void GoToModules_Click(object sender, RoutedEventArgs e)
        => ApplyNavigation("Modules");

    private void GoToEventLog_Click(object sender, RoutedEventArgs e)
        => ApplyNavigation("EventLog");

    private void OpenModule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string moduleId } || string.IsNullOrWhiteSpace(moduleId))
        {
            ApplyNavigation("Modules");
            return;
        }

        if (Window.GetWindow(this)?.DataContext is not MainWindowWindowViewModel shellViewModel)
        {
            return;
        }

        shellViewModel.ApplyNavigationRequest("modules", moduleId);
    }

    private void ApplyNavigation(string tag)
    {
        if (!QuickNavigationTargets.TryGetValue(tag, out var target))
        {
            return;
        }

        if (Window.GetWindow(this)?.DataContext is not MainWindowWindowViewModel shellViewModel)
        {
            return;
        }

        shellViewModel.ApplyNavigationRequest(target.PageId, target.TargetId);
    }
}
