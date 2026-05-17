using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Helix_Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Helix_Studio.Views;

/// <summary>
/// 主視窗。
/// </summary>
public partial class MainWindow : Window
{
    private readonly Dictionary<string, Func<UserControl>> _pageFactories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Home"] = static () => new OverviewPage(),
        ["EventLog"] = static () => new EventLogPage(),
        ["Modules"] = static () => new ModulesPage(),
        ["Settings"] = static () => new SettingsPage(),
    };

    private readonly Dictionary<string, UserControl> _pages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 建立主視窗。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        var provider = ((App)Application.Current).QueryServiceProvider();
        var viewModel = provider.GetRequiredService<MainWindowViewModel>();
        DataContext = viewModel;
        viewModel.ApplyViewLoaded();
        ApplyContent("Home");
    }

    /// <summary>
    /// 視窗關閉事件。
    /// </summary>
    /// <param name="e">事件參數。</param>
    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
    }

    private void ManageWorkspacesButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag })
        {
            ApplyContent(tag);
        }
    }

    private void GlobalSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
    }

    private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
    }

    private void GlobalSearchSuggestList_MouseUp(object sender, MouseButtonEventArgs e)
    {
    }

    private void ModuleSettingsButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void NotificationButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void ApplyContent(string tag)
    {
        if (_pageFactories.TryGetValue(tag, out var factory))
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.ApplyBreadcrumb(tag);
                viewModel.SetContentModuleId(null);
            }

            MainContent.Content = QueryOrCreatePage(tag, factory);
            return;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.ApplyBreadcrumb(tag);
            vm.SetContentModuleId(tag);
        }

        MainContent.Content = new TextBlock
        {
            Text = "目前未啟用模組 SDK，模組畫面載入功能尚未提供。",
            Margin = new Thickness(16),
            TextWrapping = TextWrapping.Wrap,
        };
    }

    /// <summary>
    /// 由內部元件要求導覽至指定模組。
    /// </summary>
    /// <param name="moduleId">模組識別碼。</param>
    internal void ApplyNavigateToModule(string moduleId) => ApplyContent(moduleId);

    private UserControl QueryOrCreatePage(string tag, Func<UserControl> factory)
    {
        if (_pages.TryGetValue(tag, out var existing))
        {
            return existing;
        }

        var created = factory();
        _pages[tag] = created;
        return created;
    }
}

