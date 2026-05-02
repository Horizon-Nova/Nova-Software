namespace Helix_Studio.ViewModels;

using System;
using Helix_Studio.ViewModels.Base;

/// <summary>
/// 工作區對話框的 UI 狀態機，負責欄位狀態、驗證訊息與關閉指令。
/// </summary>
public sealed class WorkspaceDialogViewModel : ViewModelBase
{
    private string _workspaceName = string.Empty;
    private string _workspaceDescription = string.Empty;
    private string _errorMessage = string.Empty;

    /// <summary>
    /// 建立工作區對話框 ViewModel。
    /// </summary>
    /// <param name="editName">編輯模式：既有工作區名稱；建立模式：null。</param>
    /// <param name="editDesc">編輯模式：既有工作區描述；建立模式：null。</param>
    public WorkspaceDialogViewModel(string? editName = null, string? editDesc = null)
    {
        if (editName is not null)
        {
            DialogTitleText = "Edit Workspace  ·  編輯工作區";
            WorkspaceName = editName;
            WorkspaceDescription = editDesc ?? string.Empty;
        }
        else
        {
            DialogTitleText = "New Workspace  ·  新增工作區";
        }

        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
    }

    /// <summary>
    /// 要求 View 關閉對話框；參數為 DialogResult。
    /// </summary>
    public event Action<bool?>? CloseRequested;

    /// <summary>對話框標題文字（允許雙語）。</summary>
    public string DialogTitleText { get; }

    /// <summary>工作區名稱欄位。</summary>
    public string WorkspaceName
    {
        get => _workspaceName;
        set
        {
            if (!SetProperty(ref _workspaceName, value ?? string.Empty))
                return;

            if (!string.IsNullOrEmpty(ErrorMessage))
                ErrorMessage = string.Empty;
        }
    }

    /// <summary>工作區描述欄位。</summary>
    public string WorkspaceDescription
    {
        get => _workspaceDescription;
        set
        {
            if (!SetProperty(ref _workspaceDescription, value ?? string.Empty))
                return;

            RaisePropertyChanged(nameof(DescriptionCountText));
        }
    }

    /// <summary>描述字數顯示文字。</summary>
    public string DescriptionCountText => $"{WorkspaceDescription.Length} / 120";

    /// <summary>是否存在驗證錯誤。</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>驗證錯誤訊息（允許雙語）。</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value ?? string.Empty))
                return;

            RaisePropertyChanged(nameof(HasError));
        }
    }

    /// <summary>確認（儲存）按鈕指令。</summary>
    public RelayCommand ConfirmCommand { get; }

    /// <summary>取消按鈕指令。</summary>
    public RelayCommand CancelCommand { get; }

    private void Confirm()
    {
        var name = (WorkspaceName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            ErrorMessage = "Name cannot be empty.  名稱不可為空。";
            return;
        }

        WorkspaceName = name;
        ErrorMessage = string.Empty;
        CloseRequested?.Invoke(true);
    }
}

