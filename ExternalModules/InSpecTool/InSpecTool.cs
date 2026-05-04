using System.Windows;
using ExternalModules.InSpecTool.Services;
using ExternalModules.InSpecTool.ViewModels;
using ExternalModules.InSpecTool.Views;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.InSpecTool;

/// <summary>
/// Entry point for the InSpecTool module.
/// Implements both <see cref="IModule"/> (main view) and
/// <see cref="IModuleWithSettings"/> (settings panel).
/// </summary>
public sealed class InSpecTool : IModuleWithSettings
{
    /// <summary>
    /// Settings key under which the shell stores the module's license key.
    /// Must match <c>ModuleRuntimeServices.LicenseKeySettingsKey</c>.
    /// </summary>
    private const string LicenseKeySettingsKey = "_license_key";

    private readonly InSpecToolServices _services = new();
    private readonly InSpecToolPageViewModel _pageViewModel;
    private readonly InSpecToolSettingsPageViewModel _settingsPageViewModel;
    private InSpecToolPage? _page;
    private InSpecToolSettingsPage? _settingsPage;

    /// <summary>
    /// 建立 InSpecTool 模組實例。
    /// </summary>
    public InSpecTool()
    {
        _pageViewModel = new InSpecToolPageViewModel(_services);
        _settingsPageViewModel = new InSpecToolSettingsPageViewModel(_services);
    }

    // ── IModule ────────────────────────────────────────────────────────────

    /// <summary>
    /// 建立主頁 View。
    /// </summary>
    /// <param name="context">宿主上下文。</param>
    /// <returns>主頁 WPF 元件。</returns>
    public FrameworkElement CreateView(IShellContext context)
    {
        _services.ApplyShellContext(context);
        _page ??= new InSpecToolPage(_pageViewModel);
        _pageViewModel.ApplyShellContext();
        return _page;
    }

    /// <summary>
    /// 當模組被啟用時更新授權狀態與 ViewModel 上下文。
    /// </summary>
    /// <param name="context">宿主上下文。</param>
    public void OnActivated(IShellContext context)
    {
        var storedKey = context.Settings.QueryString(LicenseKeySettingsKey);
        var result = context.VerifyModuleLicense(storedKey);

        if (result.IsValid)
        {
            context.ReportHealth(ModuleHealth.Healthy);
        }
        else
        {
            context.ReportHealth(ModuleHealth.Faulted,
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "授權金鑰無效"
                    : result.ErrorMessage);
        }

        _services.ApplyShellContext(context);
        _pageViewModel.ApplyShellContext();
        _settingsPageViewModel.LoadSettings();
    }

    /// <summary>
    /// 當模組被停用時清除目前上下文。
    /// </summary>
    public void OnDeactivated()
    {
        _pageViewModel.ClearShellContext();
        _services.ApplyShellContext(null);
    }

    // ── IModuleWithSettings ────────────────────────────────────────────────

    /// <summary>
    /// 建立設定頁 View。
    /// </summary>
    /// <param name="context">宿主上下文。</param>
    /// <returns>設定頁 WPF 元件。</returns>
    public FrameworkElement CreateSettingsView(IShellContext context)
    {
        _services.ApplyShellContext(context);
        _settingsPage ??= new InSpecToolSettingsPage(_settingsPageViewModel);
        _settingsPageViewModel.LoadSettings();
        return _settingsPage;
    }

    // ── IDisposable ────────────────────────────────────────────────────────

    /// <summary>
    /// 釋放模組資源。
    /// </summary>
    public void Dispose()
    {
        _pageViewModel.Dispose();
        _services.Dispose();
        _page = null;
        _settingsPage = null;
    }
}
