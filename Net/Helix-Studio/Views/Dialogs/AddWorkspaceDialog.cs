using System.Windows;
using System.Windows.Media;
using Helix_Studio.ViewModels;

namespace Helix_Studio.Views.Dialogs;

/// <summary>FL-02 N-004：輸入新工作區名稱，回傳 null 表示取消。</summary>
public sealed partial class AddWorkspaceDialog : Window
{
    private string? _result;

    private AddWorkspaceDialog()
    {
        InitializeComponent();

        NameBox.TextChanged += (_, _) =>
            ConfirmBtn.IsEnabled = NameBox.Text.Trim().Length > 0;

        ConfirmBtn.Click += (_, _) =>
        {
            _result = NameBox.Text.Trim();
            DialogResult = true;
        };

        var scale = GetCurrentFontScale();
        if (scale != 1.0)
            LayoutRoot.LayoutTransform = new ScaleTransform(scale, scale);

        Loaded += (_, _) => NameBox.Focus();
    }

    private static double GetCurrentFontScale()
    {
        if (Application.Current?.MainWindow?.DataContext is MainViewModel vm)
            return vm.Settings.FontScale;
        return 1.0;
    }

    /// <summary>顯示對話框，回傳使用者輸入的名稱；取消時回傳 null。</summary>
    public static string? Show(Window? owner)
    {
        var dlg = new AddWorkspaceDialog();
        if (owner is not null) dlg.Owner = owner;
        dlg.ShowDialog();
        return dlg._result;
    }
}
