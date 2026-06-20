using System.Windows;
using HorizonNova.AITrainingModule.ViewModels;
using HorizonNova.AITrainingModule.Views;
using HorizonNova.HelixStudio.ModuleSdk;
using Microsoft.Extensions.Logging;

namespace HorizonNova.AITrainingModule;

public sealed class AITrainingModule : IModule
{
    private AITrainingView? _view;
    private AITrainingViewModel? _viewModel;

    public FrameworkElement CreateView(IShellContext context)
    {
        _viewModel = new AITrainingViewModel(context);
        _view = new AITrainingView(_viewModel);
        return _view;
    }

    public void OnActivated(IShellContext context)
    {
        context.Log?.Invoke("AI Training module activated.", LogLevel.Information);
    }

    public void OnDeactivated()
    {
        _viewModel?.OnDeactivated();
    }

    public void Dispose()
    {
        _viewModel?.Dispose();
        _view = null;
        _viewModel = null;
    }
}
