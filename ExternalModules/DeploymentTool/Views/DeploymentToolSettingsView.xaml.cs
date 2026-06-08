using System.Windows.Controls;
using ExternalModules.DeploymentTool.ViewModels;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.DeploymentTool.Views;

public partial class DeploymentToolSettingsView : UserControl
{
    private readonly DeploymentToolSettingsPageViewModel _viewModel = new();

    public DeploymentToolSettingsView()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    internal void ApplyHostContext(IShellContext context)
    {
        _viewModel.ApplySettings(context.Settings);
    }

    internal void ClearHostContext()
    {
        _viewModel.ClearSettings();
    }
}
