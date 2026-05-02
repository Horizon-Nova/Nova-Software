using System.Windows;
using HorizonNova.HelixStudio.ModuleSdk;
using HorizonNova.SampleModule.Views;

namespace HorizonNova.SampleModule;

/// <summary>
/// Entry point for the Sample Module.
/// The HN-Studio shell discovers this class because it implements IModule.
/// </summary>
public sealed class SampleModule : IModule
{
    private SampleModuleView? _view;
    private IShellContext?    _context;

    public FrameworkElement CreateView(IShellContext context)
    {
        _context = context;
        _view    = new SampleModuleView();
        _view.Initialize(context);
        return _view;
    }

    public void OnActivated(IShellContext context)
    {
        _context = context;
        context.Log("Sample Module activated.");
    }

    public void OnDeactivated()
    {
        _context?.Log("Sample Module deactivated.");
    }

    public void Dispose()
    {
        _view = null;
    }
}

