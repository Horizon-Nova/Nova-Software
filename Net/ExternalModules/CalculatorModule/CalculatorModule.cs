using System.Windows;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.CalculatorModule;

public sealed class CalculatorModule : IModule
{
    private readonly Views.CalculatorView _view = new();

    public FrameworkElement CreateView(IShellContext context)
    {
        _view.ApplyHostContext(context);
        return _view;
    }

    public void OnActivated(IShellContext context)
    {
        _view.ApplyHostContext(context);
    }

    public void OnDeactivated()
    {
        _view.ClearHostContext();
    }

    public void Dispose()
    {
    }
}
