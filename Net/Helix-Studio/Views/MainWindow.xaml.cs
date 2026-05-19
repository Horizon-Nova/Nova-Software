using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Helix_Studio.AppHost;
using Helix_Studio.Components;
using Helix_Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Helix_Studio.Views;

/// <summary>
/// 主視窗（Shell）。
/// </summary>
public partial class MainWindow : Window
{
    private static readonly IReadOnlyDictionary<string, Func<UserControl>> PageFactories =
        new Dictionary<string, Func<UserControl>>(StringComparer.OrdinalIgnoreCase)
        {
            ["overview"] = static () => new OverviewPage(),
            ["eventlog"] = static () => new EventLogPage(),
            ["modules"] = static () => new ModulesPage(),
            ["settings"] = static () => new SettingsPage(),
        };

    private readonly Dictionary<string, UserControl> _pages = new(StringComparer.OrdinalIgnoreCase);
    private readonly NavigationRuntime _navigationRuntime;
    private readonly NotificationRuntime _notificationRuntime;
    private readonly InactivityRuntime _inactivityRuntime;

    public MainWindow()
    {
        InitializeComponent();

        var provider = ((App)Application.Current).QueryServiceProvider();
        _navigationRuntime = provider.GetRequiredService<NavigationRuntime>();
        _navigationRuntime.NavigationRequested += OnNavigationRequested;
        _notificationRuntime = provider.GetRequiredService<NotificationRuntime>();
        _notificationRuntime.NotificationRequested += OnNotificationRequested;
        _inactivityRuntime = provider.GetRequiredService<InactivityRuntime>();
        _inactivityRuntime.LockRequested += OnLockRequested;

        var viewModel = provider.GetRequiredService<MainWindowWindowViewModel>();
        DataContext = viewModel;
        viewModel.ApplyViewLoaded();
    }

    protected override void OnClosed(EventArgs e)
    {
        _navigationRuntime.NavigationRequested -= OnNavigationRequested;
        _notificationRuntime.NotificationRequested -= OnNotificationRequested;
        _inactivityRuntime.LockRequested -= OnLockRequested;

        foreach (var page in _pages.Values)
        {
            (page.DataContext as IDisposable)?.Dispose();
        }

        (DataContext as IDisposable)?.Dispose();
        base.OnClosed(e);
    }

    private void OnNavigationRequested(object? sender, NavigationRequestEventArgs e)
        => ApplyContent(e.PageId, e.ModuleId);

    private void OnNotificationRequested(object? sender, NotificationRequestedEventArgs e)
    {
        var request = e.Request;
        var severity = request.Severity switch
        {
            NotificationSeverity.Success => ToastSeverity.Success,
            NotificationSeverity.Error => ToastSeverity.Error,
            NotificationSeverity.Warning => ToastSeverity.Warning,
            _ => ToastSeverity.Information,
        };

        Toast.Show(this, string.IsNullOrWhiteSpace(request.Title) ? null : request.Title, request.Message, severity, request.DisplayMilliseconds);

        ApplyNotificationAction(request);
    }

    private static void ApplyNotificationAction(NotificationRequest request)
    {
        if (request.Action == NotificationAction.None)
        {
            return;
        }

        var path = request.ActionPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        switch (request.Action)
        {
            case NotificationAction.RevealInFolder:
                RevealInFolder(path);
                break;
            case NotificationAction.OpenFolder:
                OpenFolder(path);
                break;
        }
    }

    private static void RevealInFolder(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var args = string.Format(CultureInfo.InvariantCulture, "/select,\"{0}\"", filePath);
        _ = Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = args,
            UseShellExecute = true,
        });
    }

    private static void OpenFolder(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        _ = Process.Start(new ProcessStartInfo
        {
            FileName = directoryPath,
            UseShellExecute = true,
        });
    }

    private void ApplyContent(string pageId, string? moduleId)
    {
        if (PageFactories.TryGetValue(pageId, out var factory))
        {
            var page = QueryOrCreatePage(pageId, factory);
            MainContent.Content = page;

            if (page is ModulesPage modulesPage && !string.IsNullOrWhiteSpace(moduleId))
            {
                modulesPage.ApplySelectModule(moduleId);
            }

            return;
        }

        MainContent.Content = CreatePlaceholder(pageId, moduleId);
    }

    private void OnLockRequested(object? sender, EventArgs e)
    {
        _inactivityRuntime.ApplyLocked();

        var lockWindow = new LockWindow
        {
            Owner = this,
        };

        lockWindow.ShowDialog();
        _inactivityRuntime.ApplyUnlocked();
    }

    /// <summary>
    /// 供模組頁請求：切換到模組頁並選取指定模組。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    internal void ApplyNavigateToModule(string moduleId)
    {
        if (DataContext is MainWindowWindowViewModel viewModel)
        {
            viewModel.NavigateCommand.Execute("modules");
        }

        ApplyContent("modules", moduleId);
    }

    private UserControl QueryOrCreatePage(string pageId, Func<UserControl> factory)
    {
        if (_pages.TryGetValue(pageId, out var existing))
        {
            return existing;
        }

        var created = factory();
        _pages[pageId] = created;
        return created;
    }

    private static UIElement CreatePlaceholder(string pageId, string? moduleId)
    {
        var label = string.IsNullOrWhiteSpace(moduleId) ? pageId : moduleId;
        return new TextBlock
        {
            Text = $"尚未實作頁面：{label}",
            Margin = new Thickness(24),
            TextWrapping = TextWrapping.Wrap,
        };
    }
}
