using System;







using System.Collections.Generic;







using System.Diagnostics;







using System.Globalization;







using System.IO;







using System.Windows;







using System.Windows.Controls;







using System.Windows.Controls.Primitives;







using System.Windows.Media;







using System.Windows.Media.Animation;







using Helix_Studio.AppRuntime;







using Helix_Studio.Components;







using Helix_Studio.Infrastructure.Modules;







using Helix_Studio.ViewModels;







using Microsoft.Extensions.DependencyInjection;















namespace Helix_Studio.Views;















/// <summary>







/// 主視窗（Shell）。







/// </summary>







public partial class MainWindow : Window







{







    private const double ModuleSettingsDrawerMinWidth = 420d;







    private const double ModuleSettingsDrawerMaxWidth = 760d;







    private double _moduleSettingsDrawerWidth = 540d;







    private static readonly Duration ModuleSettingsDrawerAnimationDuration = TimeSpan.FromMilliseconds(220);















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







    private readonly ModuleRuntimeService _moduleRuntimeService;







    private string? _moduleSettingsDrawerModuleId;















    /// <summary>







    /// 建立主視窗並掛上 Runtime 與 ViewModel。







    /// </summary>







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







        _moduleRuntimeService = provider.GetRequiredService<ModuleRuntimeService>();















        var viewModel = provider.GetRequiredService<MainWindowWindowViewModel>();







        DataContext = viewModel;







        viewModel.ApplyViewLoaded();







    }















    /// <summary>







    /// 視窗關閉時解除事件訂閱並釋放頁面資源。







    /// </summary>







    /// <param name="e">關閉事件參數。</param>







    protected override void OnClosed(EventArgs e)







    {







        _navigationRuntime.NavigationRequested -= OnNavigationRequested;







        _notificationRuntime.NotificationRequested -= OnNotificationRequested;







        _inactivityRuntime.LockRequested -= OnLockRequested;







        CloseModuleSettingsDrawer(animate: false);







        _moduleRuntimeService.DeactivateCurrentModule();















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







        if (TryResolveModulePageId(pageId, out var runtimeModuleId))







        {







            CloseModuleSettingsDrawer(animate: false);







            try







            {







                var page = _moduleRuntimeService.CreateModuleView(runtimeModuleId);







                var isHealthy = _moduleRuntimeService.ActivateModule(runtimeModuleId);







                MainContent.Content = isHealthy ? page : CreateLicenseRequiredPlaceholder(runtimeModuleId);







            }







            catch (Exception ex)







            {







                Trace.WriteLine(ex);







                MainContent.Content = CreatePlaceholder(pageId, ex.Message);







            }















            return;







        }















        if (TryResolveModuleSettingsPageId(pageId, out var settingsModuleId))







        {







            try







            {







                var page = _moduleRuntimeService.CreateSettingsView(settingsModuleId);







                _moduleRuntimeService.ActivateModule(settingsModuleId);







                MainContent.Content = page ?? CreatePlaceholder(pageId, "此模組未提供設定畫面。");







            }







            catch (Exception ex)







            {







                Trace.WriteLine(ex);







                MainContent.Content = CreatePlaceholder(pageId, ex.Message);







            }















            return;







        }















        CloseModuleSettingsDrawer();







        _moduleRuntimeService.DeactivateCurrentModule();















        if (PageFactories.TryGetValue(pageId, out var factory))







        {







            var page = QueryOrCreatePage(pageId, factory);







            MainContent.Content = page;















            if (page is ModulesPage modulesPage && !string.IsNullOrWhiteSpace(moduleId))







            {







                modulesPage.ApplySelectModule(moduleId);







            }















            if (page is SettingsPage settingsPage && !string.IsNullOrWhiteSpace(moduleId))







            {







                settingsPage.ApplySectionTag(moduleId);







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







            viewModel.ApplyNavigationRequest(MainWindowWindowViewModel.BuildModuleNavigationId(moduleId));







        }







    }















    /// <summary>







    /// 導向模組設定頁面。







    /// </summary>







    /// <param name="moduleId">模組識別碼。</param>







    /// <param name="moduleDisplayName">模組顯示名稱，用於抽屜標題</param>







    internal void ApplyNavigateToModuleSettings(string moduleId, string? moduleDisplayName = null)







        => OpenModuleSettingsDrawer(moduleId, moduleDisplayName);















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



    private static UIElement CreateLicenseRequiredPlaceholder(string moduleId)
        => new TextBlock
        {
            Text = $"模組 [{moduleId}] 需要有效的授權金鑰才能使用。\n請點擊右上角的設定圖示，在「模組授權」欄位輸入金鑰後按「套用授權」。",
            Margin = new Thickness(24),
            TextWrapping = TextWrapping.Wrap,
        };

    private void OpenModuleSettingsDrawer(string moduleId, string? moduleDisplayName = null)



    {



        if (string.IsNullOrWhiteSpace(moduleId))



        {



            return;



        }







        UIElement content;



        try



        {



            _moduleSettingsDrawerModuleId = moduleId;



            content = _moduleRuntimeService.CreateSettingsView(moduleId) ?? CreatePlaceholder(moduleId, "??????????");



            _moduleRuntimeService.ActivateModule(moduleId);



        }



        catch (Exception ex)



        {



            Trace.WriteLine(ex);



            _moduleSettingsDrawerModuleId = null;



            content = CreatePlaceholder(moduleId, ex.Message);



        }







        ModuleSettingsDrawerContentHost.Content = content;



        ModuleSettingsDrawerTitle.Text = string.IsNullOrWhiteSpace(moduleDisplayName) ? moduleId : moduleDisplayName;



        ModuleSettingsDrawerSubtitle.Text = $"ID: {moduleId}";







        ModuleSettingsDrawerOverlay.Visibility = Visibility.Visible;



        ModuleSettingsDrawerPanel.Visibility = Visibility.Visible;



        ModuleSettingsDrawerTransform.BeginAnimation(TranslateTransform.XProperty, null);



        _moduleSettingsDrawerWidth = Math.Clamp(_moduleSettingsDrawerWidth, ModuleSettingsDrawerMinWidth, ModuleSettingsDrawerMaxWidth);



        ModuleSettingsDrawerPanel.Width = _moduleSettingsDrawerWidth;



        ModuleSettingsDrawerTransform.X = _moduleSettingsDrawerWidth;







        var animation = new DoubleAnimation



        {



            From = _moduleSettingsDrawerWidth,



            To = 0,



            Duration = ModuleSettingsDrawerAnimationDuration,



            EasingFunction = new CubicEase



            {



                EasingMode = EasingMode.EaseOut,



            },



        };







        ModuleSettingsDrawerTransform.BeginAnimation(TranslateTransform.XProperty, animation);






    }















    private void CloseModuleSettingsDrawer(bool animate = true)







    {







        if (ModuleSettingsDrawerOverlay.Visibility != Visibility.Visible)







        {







            return;







        }















        var moduleId = _moduleSettingsDrawerModuleId;







        _moduleSettingsDrawerModuleId = null;















        void HideDrawer()







        {







            ModuleSettingsDrawerContentHost.Content = null;







            ModuleSettingsDrawerOverlay.Visibility = Visibility.Collapsed;







            ModuleSettingsDrawerPanel.Visibility = Visibility.Collapsed;







            ModuleSettingsDrawerTransform.BeginAnimation(TranslateTransform.XProperty, null);







            ModuleSettingsDrawerPanel.Width = _moduleSettingsDrawerWidth;







            ModuleSettingsDrawerTransform.X = _moduleSettingsDrawerWidth;







            if (!string.IsNullOrWhiteSpace(moduleId))







            {







                _moduleRuntimeService.DeactivateCurrentModule();







            }







        }















        if (!animate)







        {







            HideDrawer();







            return;







        }















        var animation = new DoubleAnimation







        {







            From = 0,







            To = _moduleSettingsDrawerWidth,







            Duration = ModuleSettingsDrawerAnimationDuration,







            EasingFunction = new CubicEase







            {







                EasingMode = EasingMode.EaseIn,







            },







        };















        animation.Completed += (_, _) => HideDrawer();







        ModuleSettingsDrawerTransform.BeginAnimation(TranslateTransform.XProperty, animation);







    }















    private void ModuleSettingsDrawerResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)







    {







        var nextWidth = Math.Clamp(ModuleSettingsDrawerPanel.Width + (-e.HorizontalChange), ModuleSettingsDrawerMinWidth, ModuleSettingsDrawerMaxWidth);







        if (Math.Abs(nextWidth - ModuleSettingsDrawerPanel.Width) < double.Epsilon)







        {







            return;







        }















        _moduleSettingsDrawerWidth = nextWidth;







        ModuleSettingsDrawerPanel.Width = nextWidth;















        if (ModuleSettingsDrawerOverlay.Visibility == Visibility.Visible)







        {







            ModuleSettingsDrawerTransform.BeginAnimation(TranslateTransform.XProperty, null);







            ModuleSettingsDrawerTransform.X = 0;







        }







    }















    private void ModuleSettingsDrawerScrim_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)







    {







        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)







        {







            CloseModuleSettingsDrawer();







        }







    }















    private void ModuleSettingsDrawerCloseButton_Click(object sender, RoutedEventArgs e)







        => CloseModuleSettingsDrawer();















    private static bool TryResolveModulePageId(string pageId, out string moduleId)







    {







        const string prefix = "module:";







        if (pageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))







        {







            moduleId = pageId[prefix.Length..];







            return !string.IsNullOrWhiteSpace(moduleId);







        }















        moduleId = string.Empty;







        return false;







    }















    private static bool TryResolveModuleSettingsPageId(string pageId, out string moduleId)







    {







        const string prefix = "module-settings:";







        if (pageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))







        {







            moduleId = pageId[prefix.Length..];







            return !string.IsNullOrWhiteSpace(moduleId);







        }















        moduleId = string.Empty;







        return false;







    }







}







