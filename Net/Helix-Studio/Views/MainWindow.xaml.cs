using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Helix_Studio.Components;
using Helix_Studio.Model.Modules;
using Helix_Studio.Model.Navigation;
using Helix_Studio.Model.Notifications;
using Helix_Studio.ViewModels;

namespace Helix_Studio.Views;

/// <summary>應用程式主視窗：內容區導覽、模組裝載、通知與閒置鎖定。</summary>
public partial class MainWindow : Window
{
    private sealed record PageDef(string Breadcrumb, string Title, Func<UIElement> Factory);

    private readonly Dictionary<string, PageDef> _pages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Home"] = new("Home", "Home", () => new OverviewPage()),
        ["EventLog"] = new("Event Log", "Event Log", () => new EventLogPage()),
        ["Modules"] = new("Modules", "Modules", () => new ModulesPage()),
        ["Settings"] = new("Settings", "Settings", () => new SettingsPage()),
    };

    private readonly MainWindowViewModel _viewModel;

    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private DateTimeOffset _lastUserInputUtc = DateTimeOffset.UtcNow;
    private bool _isLockShown;

    private readonly Dictionary<string, UIElement> _moduleViews = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, UIElement> _moduleSettingsViews = new(StringComparer.OrdinalIgnoreCase);

    private string? _currentModuleId;
    private bool _showingModuleSettings;

    private string _lastNavTag = "Home";

    private bool _suppressNavChecked;

    /// <summary>初始化主視窗、<see cref="MainWindowViewModel"/>、閒置計時與導覽相關事件。</summary>
    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        Loaded += MainWindow_ViewReady;
        Closed += MainWindow_Closed;

        _viewModel.NotificationPublished += OnNotificationPublished;
        _viewModel.ModuleCatalogChanged += OnModuleCatalogChanged;
        _viewModel.WorkspaceActivated += OnWorkspaceActivated;
        _viewModel.InitialNavigationRequested += OnInitialNavigationRequested;
        _viewModel.GlobalSearchNavigateRequested += OnGlobalSearchNavigateRequested;

        _idleTimer.Tick += (_, _) => TickInactivityLock();
        _idleTimer.Start();
    }

    /// <summary>依內部導覽標籤切換內建分頁或啟用對應模組檢視，並更新上次造訪記錄。</summary>
    public void NavigateTo(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;

        _lastNavTag = tag;
        _viewModel.PersistLastVisitedCommand.Execute(tag);

        if (TryNavigateModule(tag))
            return;

        if (!_pages.TryGetValue(tag, out var page))
            page = new PageDef(tag, tag, () => MakePlaceholder(tag, tag));

        MainContent.Content = page.Factory();
        SyncNavChecked(tag);
        UpdateNavigationChromeForBuiltInPage(page);
    }

    private void MainWindow_ViewReady(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadedCommand.Execute(null);
    }

    private void OnInitialNavigationRequested(string tag)
    {
        NavigateTo(tag);
    }

    private void OnGlobalSearchNavigateRequested(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;

        NavigateTo(tag);
        GlobalSearchBox.Clear();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _viewModel.NotificationPublished -= OnNotificationPublished;
        _viewModel.ModuleCatalogChanged -= OnModuleCatalogChanged;
        _viewModel.WorkspaceActivated -= OnWorkspaceActivated;
        _viewModel.InitialNavigationRequested -= OnInitialNavigationRequested;
        _viewModel.GlobalSearchNavigateRequested -= OnGlobalSearchNavigateRequested;
        _viewModel.Dispose();
    }

    private void OnNotificationPublished(ShellNotificationMessage message)
    {
        var severity = message.Level switch
        {
            ShellNotificationLevel.Warning => ToastSeverity.Warning,
            ShellNotificationLevel.Error => ToastSeverity.Error,
            _ => ToastSeverity.Information,
        };

        Toast.Show(this, message.TitleText, message.MessageText, severity, message.DurationMilliseconds);
    }

    private void OnModuleCatalogChanged()
    {
        _moduleViews.Clear();
        _moduleSettingsViews.Clear();

        if (_currentModuleId is not null && !_viewModel.ModuleNavItems.Any(x => x.Tag.Equals($"Module:{_currentModuleId}", StringComparison.OrdinalIgnoreCase)))
        {
            _currentModuleId = null;
            _showingModuleSettings = false;
            NavigateTo("Home");
        }
    }

    private void OnWorkspaceActivated()
    {
        _moduleViews.Clear();
        _moduleSettingsViews.Clear();
        _currentModuleId = null;
        _showingModuleSettings = false;
        SyncNavChecked(_lastNavTag);
        WorkspaceBreadcrumb.Text = ResolveWorkspaceBreadcrumbText();
    }

    private bool TryNavigateModule(string tag)
    {
        if (!tag.StartsWith("Module:", StringComparison.OrdinalIgnoreCase))
            return false;

        var moduleId = tag["Module:".Length..];
        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        _currentModuleId = moduleId;
        _showingModuleSettings = false;

        var view = GetOrActivateModuleView(moduleId);
        MainContent.Content = view;
        SyncNavChecked(tag);
        UpdateNavigationChromeForModule(tag, moduleId);
        return true;
    }

    private UIElement GetOrActivateModuleView(string moduleId)
    {
        if (_moduleViews.TryGetValue(moduleId, out var cached))
            return cached;

        _viewModel.ActivateModuleCommand.Execute(moduleId);
        var result = _viewModel.LastShellModuleActivation;

        if (result?.Status == ShellModuleActivationStatus.LicenseRequired && result.LicenseResult is not null)
        {
            _viewModel.ResolveModuleManifestCommand.Execute(moduleId);
            var manifest = _viewModel.LastResolvedModuleManifest;
            var displayName = manifest?.DisplayName ?? moduleId;
            var error = string.IsNullOrWhiteSpace(result.LicenseResult.ErrorMessage)
                ? "授權驗證失敗。"
                : result.LicenseResult.ErrorMessage;

            if (ShowModuleLicenseDialog(moduleId, displayName, error))
            {
                _viewModel.ActivateModuleCommand.Execute(moduleId);
                result = _viewModel.LastShellModuleActivation;
            }
        }

        if (result?.Status == ShellModuleActivationStatus.Success && result.View is UIElement ui)
        {
            _moduleViews[moduleId] = ui;
            return ui;
        }

        _viewModel.LogModuleActivationFailedCommand.Execute(moduleId);
        return MakePlaceholder("Module", "模組");
    }

    private UIElement GetOrActivateModuleSettingsView(string moduleId)
    {
        if (_moduleSettingsViews.TryGetValue(moduleId, out var cached))
            return cached;

        _viewModel.RefreshModuleCapabilitiesCommand.Execute(moduleId);
        if (!_viewModel.LastModuleHasSettings)
            return MakePlaceholder("Settings", "設定");

        _viewModel.ActivateModuleSettingsCommand.Execute(moduleId);
        var view = _viewModel.LastModuleSettingsView;
        if (view is UIElement ui)
        {
            _moduleSettingsViews[moduleId] = ui;
            return ui;
        }

        return MakePlaceholder("Settings", "設定");
    }

    private bool ShowModuleLicenseDialog(string moduleId, string displayName, string initialError)
    {
        var dialog = new ModuleLicenseDialog(moduleId, displayName, initialError)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true) return false;

        _viewModel.SaveModuleLicenseKeyCommand.Execute(new SaveModuleLicensePayload(moduleId, dialog.LicenseKey));
        return true;
    }

    private void TickInactivityLock()
    {
        if (_isLockShown) return;

        var timeout = _viewModel.InactivityLockDuration;
        if (timeout <= TimeSpan.Zero) return;

        var elapsed = DateTimeOffset.UtcNow - _lastUserInputUtc;
        if (elapsed < timeout) return;

        ShowLockWindow();
    }

    private void ShowLockWindow()
    {
        _isLockShown = true;
        try
        {
            var lockWindow = new LockWindow { Owner = this };
            lockWindow.ShowDialog();
        }
        finally
        {
            _isLockShown = false;
            _lastUserInputUtc = DateTimeOffset.UtcNow;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        _lastUserInputUtc = DateTimeOffset.UtcNow;

        if (e.Key == Key.F1)
        {
            _viewModel.OpenOfflineHelpCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void WorkspaceSwitchButton_Click(object sender, RoutedEventArgs e)
    {
        WorkspacePopup.IsOpen = true;
        _lastUserInputUtc = DateTimeOffset.UtcNow;
    }

    private void ManageWorkspacesButton_Click(object sender, RoutedEventArgs e)
    {
        WorkspacePopup.IsOpen = false;
        NavigateTo("Settings");
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressNavChecked) return;
        if (sender is not RadioButton rb) return;
        if (rb.IsChecked != true) return;
        if (rb.Tag is not string tag) return;
        NavigateTo(tag);
    }

    private void ModuleSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentModuleId)) return;

        _showingModuleSettings = !_showingModuleSettings;
        MainContent.Content = _showingModuleSettings
            ? GetOrActivateModuleSettingsView(_currentModuleId)
            : GetOrActivateModuleView(_currentModuleId);
    }

    private void NotificationButton_Click(object sender, RoutedEventArgs e)
    {
        NotificationPopup.IsOpen = true;
        _viewModel.MarkAllReadCommand.Execute(null);
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenOfflineHelpCommand.Execute(null);
    }

    private void GlobalSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteSearch(GlobalSearchBox.Text);
            e.Handled = true;
            return;
        }

        if (!GlobalSearchPopup.IsOpen) return;

        if (e.Key == Key.Down)
        {
            var next = GlobalSearchSuggestList.SelectedIndex + 1;
            if (next >= GlobalSearchSuggestList.Items.Count) next = GlobalSearchSuggestList.Items.Count - 1;
            GlobalSearchSuggestList.SelectedIndex = next;
            GlobalSearchSuggestList.ScrollIntoView(GlobalSearchSuggestList.SelectedItem);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            var prev = GlobalSearchSuggestList.SelectedIndex - 1;
            if (prev < 0) prev = 0;
            GlobalSearchSuggestList.SelectedIndex = prev;
            GlobalSearchSuggestList.ScrollIntoView(GlobalSearchSuggestList.SelectedItem);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            GlobalSearchPopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var q = GlobalSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(q))
        {
            SearchResultBadge.Visibility = Visibility.Collapsed;
            GlobalSearchPopup.IsOpen = false;
            return;
        }

        _viewModel.RefreshGlobalSearchCommand.Execute(q);

        SearchResultCount.Text = _viewModel.GlobalSearchDistinctTagCount.ToString();
        SearchResultBadge.Visibility = _viewModel.GlobalSearchDistinctTagCount > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        GlobalSearchSuggestList.ItemsSource = _viewModel.GlobalSearchSuggestions;
        GlobalSearchSuggestList.SelectedIndex = _viewModel.GlobalSearchSuggestions.Count > 0 ? 0 : -1;
        GlobalSearchPopup.IsOpen = _viewModel.GlobalSearchSuggestions.Count > 0;
    }

    private void GlobalSearchSuggestList_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (GlobalSearchSuggestList.SelectedItem is MainSearchSuggestion sug)
            AcceptGlobalSuggestion(sug);
    }

    private void AcceptGlobalSuggestion(MainSearchSuggestion sug)
    {
        NavigateTo(sug.Tag);
        GlobalSearchPopup.IsOpen = false;
        GlobalSearchBox.Clear();
    }

    private void ExecuteSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        _viewModel.NavigateFromGlobalSearchCommand.Execute(query);
    }

    private void SyncNavChecked(string tag)
    {
        _suppressNavChecked = true;
        try
        {
            ClearModuleNavRadioChecks();
            NavHome.IsChecked = false;
            NavEventLog.IsChecked = false;
            NavModules.IsChecked = false;
            NavSettings.IsChecked = false;

            if (tag.StartsWith("Module:", StringComparison.OrdinalIgnoreCase))
            {
                if (!TrySelectModuleNavRadio(tag))
                {
                    var deferred = tag;
                    Dispatcher.BeginInvoke(
                        DispatcherPriority.Loaded,
                        () =>
                        {
                            _suppressNavChecked = true;
                            try
                            {
                                ClearModuleNavRadioChecks();
                                NavHome.IsChecked = false;
                                NavEventLog.IsChecked = false;
                                NavModules.IsChecked = false;
                                NavSettings.IsChecked = false;
                                TrySelectModuleNavRadio(deferred);
                            }
                            finally
                            {
                                _suppressNavChecked = false;
                            }
                        });
                }

                return;
            }

            if (string.Equals(tag, "Home", StringComparison.OrdinalIgnoreCase))
                NavHome.IsChecked = true;
            else if (string.Equals(tag, "EventLog", StringComparison.OrdinalIgnoreCase))
                NavEventLog.IsChecked = true;
            else if (string.Equals(tag, "Modules", StringComparison.OrdinalIgnoreCase))
                NavModules.IsChecked = true;
            else if (string.Equals(tag, "Settings", StringComparison.OrdinalIgnoreCase))
                NavSettings.IsChecked = true;
            else
                NavHome.IsChecked = true;
        }
        finally
        {
            _suppressNavChecked = false;
        }
    }

    private void ClearModuleNavRadioChecks()
    {
        foreach (var item in _viewModel.ModuleNavItems)
        {
            if (ModuleNavItems.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter presenter)
                continue;

            if (FindVisualChild<RadioButton>(presenter) is { } rb)
                rb.IsChecked = false;
        }
    }

    private bool TrySelectModuleNavRadio(string tag)
    {
        var missingContainer = false;
        foreach (var item in _viewModel.ModuleNavItems)
        {
            if (ModuleNavItems.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter presenter)
            {
                missingContainer = true;
                continue;
            }

            if (FindVisualChild<RadioButton>(presenter) is not { } rb)
            {
                missingContainer = true;
                continue;
            }

            if (rb.Tag is string t && string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))
            {
                rb.IsChecked = true;
                return true;
            }
        }

        return !missingContainer;
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private void UpdateNavigationChromeForBuiltInPage(PageDef page)
    {
        WorkspaceBreadcrumb.Text = ResolveWorkspaceBreadcrumbText();
        BreadcrumbText.Text = page.Breadcrumb;
        PageTitle.Text = page.Title;
    }

    private void UpdateNavigationChromeForModule(string tag, string moduleId)
    {
        WorkspaceBreadcrumb.Text = ResolveWorkspaceBreadcrumbText();
        var title = _viewModel.ModuleNavItems
                        .FirstOrDefault(x => string.Equals(x.Tag, tag, StringComparison.OrdinalIgnoreCase))
                        ?.DisplayName
                    ?? moduleId;
        BreadcrumbText.Text = title;
        PageTitle.Text = title;
    }

    private string ResolveWorkspaceBreadcrumbText()
    {
        var name = _viewModel.ActiveWorkspaceName;
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "—", StringComparison.Ordinal))
            return _viewModel.StatusWorkspaceText;

        return name;
    }

    private static UIElement MakePlaceholder(string enName, string zhName)
    {
        var root = new Border
        {
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{enName}  ·  {zhName}",
                        FontSize = 22,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                    new TextBlock
                    {
                        Text = "此頁面正在開發中，敬請期待。",
                        FontSize = 13,
                        Margin = new Thickness(0, 12, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                }
            }
        };

        root.SetResourceReference(Border.BackgroundProperty, "SurfaceBase");
        if (root.Child is StackPanel panel && panel.Children.Count >= 2)
        {
            if (panel.Children[0] is TextBlock title)
                title.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");

            if (panel.Children[1] is TextBlock desc)
                desc.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiary");
        }

        return root;
    }
}
