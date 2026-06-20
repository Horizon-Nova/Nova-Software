using System.Windows;
using ExternalModules.InSpecTool.App.Runtime;
using ExternalModules.InSpecTool.ViewModels;
using ExternalModules.InSpecTool.Views;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.InSpecTool;

public sealed class InSpecTool : IModuleWithSettings
{
    private readonly InSpecToolRuntime _services = new();
    private readonly InSpecToolPageViewModel _pageViewModel;
    private readonly InSpecToolSettingsPageViewModel _settingsPageViewModel;
    private InSpecToolPage? _page;
    private InSpecToolSettingsPage? _settingsPage;

    public InSpecTool()
    {
        _pageViewModel = new InSpecToolPageViewModel(_services);
        _settingsPageViewModel = new InSpecToolSettingsPageViewModel(_services);
    }

    public FrameworkElement CreateView(IShellContext context)
    {
        _services.ApplyShellContext(context);
        _page ??= new InSpecToolPage(_pageViewModel);
        _pageViewModel.ApplyShellContext();
        return _page;
    }

    public void OnActivated(IShellContext context)
    {
        _services.ApplyShellContext(context);

        var verification = _services.VerifyModuleLicenseKey(_services.QueryModuleLicenseKey());
        context.ReportHealth(
            ModuleHealth.Healthy,
            verification.IsValid
                ? "模組狀態：Healthy"
                : "模組啟動時未通過授權驗證，已要求重新輸入模組金鑰。");

        _pageViewModel.ApplyShellContext();
        _settingsPageViewModel.LoadSettings();
    }

    public void OnDeactivated()
    {
        _pageViewModel.ClearShellContext();
        _services.ApplyShellContext(null);
    }

    public FrameworkElement CreateSettingsView(IShellContext context)
    {
        _services.ApplyShellContext(context);
        _settingsPage ??= new InSpecToolSettingsPage(_settingsPageViewModel);
        _settingsPageViewModel.LoadSettings();
        return _settingsPage;
    }

    public void Dispose()
    {
        _pageViewModel.Dispose();
        _settingsPageViewModel.Dispose();
        _services.Dispose();
        _page = null;
        _settingsPage = null;
    }
}
