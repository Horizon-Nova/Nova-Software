using System.Windows;
using System.Windows.Media;
using Helix_Studio.ViewModels;

namespace Helix_Studio.Views.Dialogs;

public enum SwitchDialogResult { StopAndSwitch, ContinueRunning, Cancel }

public sealed partial class WorkspaceSwitchDialog : Window
{
    private SwitchDialogResult _result = SwitchDialogResult.Cancel;

    private WorkspaceSwitchDialog(IReadOnlyList<string> toolNames)
    {
        InitializeComponent();

        ToolList.ItemsSource = toolNames.Select(n => "• " + n).ToList();

        StopBtn.Click += (_, _) => { _result = SwitchDialogResult.StopAndSwitch; DialogResult = true; };
        ContinueBtn.Click += (_, _) => { _result = SwitchDialogResult.ContinueRunning; DialogResult = true; };

        var scale = GetCurrentFontScale();
        if (scale != 1.0)
            LayoutRoot.LayoutTransform = new ScaleTransform(scale, scale);
    }

    private static double GetCurrentFontScale()
    {
        if (Application.Current?.MainWindow?.DataContext is MainViewModel vm)
            return vm.Settings.FontScale;
        return 1.0;
    }

    public static SwitchDialogResult Show(Window owner, IReadOnlyList<string> toolNames)
    {
        var dlg = new WorkspaceSwitchDialog(toolNames) { Owner = owner };
        dlg.ShowDialog();
        return dlg._result;
    }
}
