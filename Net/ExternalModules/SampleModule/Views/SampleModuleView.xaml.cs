using System.Windows.Controls;
using HorizonNova.HelixStudio.ModuleSdk;

namespace HorizonNova.SampleModule.Views;

public partial class SampleModuleView : UserControl
{
    public SampleModuleView()
    {
        InitializeComponent();
    }

    public void Initialize(IShellContext context)
    {
        var ws = context.ActiveWorkspaceName;
        WorkspaceText.Text = string.IsNullOrWhiteSpace(ws)
            ? "目前無使用中工作區"
            : $"使用中工作區：{ws}";
    }
}

