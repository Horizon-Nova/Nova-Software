using System.Windows;
namespace Helix_Studio.Views;

/// <summary>建立或編輯工作區名稱與說明的小型對話框。</summary>
public partial class WorkspaceDialog : Window
{
    /// <summary>使用者按確認且驗證通過時的工作區名稱；否則維持預設空字串。</summary>
    public string ResultName { get; private set; } = string.Empty;

    /// <summary>使用者按確認且驗證通過時的工作區說明；否則維持預設空字串。</summary>
    public string ResultDesc { get; private set; } = string.Empty;

    /// <summary>以擁有者視窗與可選的編輯用既有名稱／說明建立對話框。</summary>
    public WorkspaceDialog(Window owner, string? editName = null, string? editDesc = null)
    {
        Owner = owner;
        InitializeComponent();

        var vm = new ViewModels.WorkspaceDialogViewModel(editName, editDesc);
        vm.CloseRequested += OnCloseRequested;
        DataContext = vm;

        TbName.Focus();
    }

    private void OnCloseRequested(bool? dialogResult)
    {
        if (DataContext is ViewModels.WorkspaceDialogViewModel vm)
        {
            vm.CloseRequested -= OnCloseRequested;

            if (dialogResult == true)
            {
                ResultName = vm.WorkspaceName.Trim();
                ResultDesc = vm.WorkspaceDescription.Trim();
            }
        }

        DialogResult = dialogResult;
        Close();
    }
}
