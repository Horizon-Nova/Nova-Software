using System.Windows;
using HorizonNova.HelixStudio.ModuleSdk;
using HorizonNova.SampleModule.ViewModels;
using HorizonNova.SampleModule.Views;
using Microsoft.Extensions.Logging;

namespace HorizonNova.SampleModule;

/// <summary>
/// Entry point for the Sample Module.
/// The HN-Studio shell discovers this class because it implements IModule.
/// </summary>
public sealed class SampleModule : IModule
{
    private SampleModuleView? _view;
    private SampleModulePageViewModel? _viewModel;
    private IShellContext? _context;

    public FrameworkElement CreateView(IShellContext context)
    {
        _context = context;
        _viewModel = new SampleModulePageViewModel();
        _viewModel.ApplyWorkspace(context.ActiveWorkspaceName);
        _view = new SampleModuleView(_viewModel);
        return _view;
    }

    public void OnActivated(IShellContext context)
    {
        _context = context;
        _viewModel?.ApplyWorkspace(context.ActiveWorkspaceName);
        context.Log?.Invoke("Sample Module activated.", LogLevel.Information);
    }

    public void OnDeactivated()
    {
        _context?.Log?.Invoke("Sample Module deactivated.", LogLevel.Information);
    }

    public void Dispose()
    {
        _view = null;
        _viewModel = null;
    }
}
