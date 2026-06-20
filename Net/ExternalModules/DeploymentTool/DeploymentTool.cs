using System.Windows;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.DeploymentTool;

public sealed class DeploymentTool : IModuleWithSettings
{
    private readonly Views.DeploymentToolView _view = new();
    private Views.DeploymentToolSettingsView? _settingsView;

    public FrameworkElement CreateView(IShellContext context)
    {
        _view.ApplyHostContext(context);
        return _view;
    }

    public void OnActivated(IShellContext context)
    {
        _view.ApplyHostContext(context);
        _settingsView?.ApplyHostContext(context);
    }

    public void OnDeactivated()
    {
        _view.ClearHostContext();
        _settingsView?.ClearHostContext();
    }

    public FrameworkElement CreateSettingsView(IShellContext context)
    {
        _settingsView ??= new Views.DeploymentToolSettingsView();
        _settingsView.ApplyHostContext(context);
        return _settingsView;
    }

    public void Dispose()
    {
    }
}
