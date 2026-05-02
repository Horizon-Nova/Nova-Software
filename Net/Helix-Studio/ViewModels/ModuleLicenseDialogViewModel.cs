using System;
using Helix_Studio.Services;
using Helix_Studio.ViewModels.Base;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 模組授權金鑰對話框的 UI 狀態機，負責輸入、驗證與錯誤訊息。
/// </summary>
public sealed class ModuleLicenseDialogViewModel : ViewModelBase
{
    private readonly ModuleLicenseServices _service;
    private readonly string _moduleId;

    private string _licenseKey = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    /// <summary>
    /// 建立模組授權金鑰對話框 ViewModel。
    /// </summary>
    public ModuleLicenseDialogViewModel(string moduleId, string displayName, string initialError = "")
        : this(AppServices.ModuleLicense, moduleId, displayName, initialError) { }

    /// <summary>
    /// 建立模組授權金鑰對話框 ViewModel。
    /// </summary>
    public ModuleLicenseDialogViewModel(
        ModuleLicenseServices service,
        string moduleId,
        string displayName,
        string initialError = "")
    {
        _service = service;
        _moduleId = moduleId;

        ModuleDisplayNameText = displayName;
        ErrorMessage = initialError ?? string.Empty;

        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false, null));
        VerifyCommand = new RelayCommand(Verify, () => !IsBusy);
    }

    /// <summary>
    /// 要求 View 關閉對話框；參數分別為 DialogResult 與成功時的金鑰。
    /// </summary>
    public event Action<bool?, string?>? CloseRequested;

    /// <summary>模組顯示名稱。</summary>
    public string ModuleDisplayNameText { get; }

    /// <summary>授權金鑰輸入欄位。</summary>
    public string LicenseKey
    {
        get => _licenseKey;
        set => SetProperty(ref _licenseKey, value ?? string.Empty);
    }

    /// <summary>是否顯示錯誤狀態。</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>錯誤訊息（允許雙語）。</summary>
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

    /// <summary>是否正在執行驗證。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
                return;

            RelayCommand.InvalidateAll();
            RaisePropertyChanged(nameof(VerifyButtonText));
        }
    }

    /// <summary>驗證按鈕文字（允許雙語）。</summary>
    public string VerifyButtonText => IsBusy ? "驗證中…" : "驗證並開啟";

    /// <summary>取消按鈕指令。</summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>驗證按鈕指令。</summary>
    public RelayCommand VerifyCommand { get; }

    private void Verify()
    {
        var key = (LicenseKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            ErrorMessage = "請輸入授權金鑰。";
            return;
        }

        IsBusy = true;
        try
        {
            var result = _service.Verify(_moduleId, key);
            if (result.IsValid)
            {
                ErrorMessage = string.Empty;
                CloseRequested?.Invoke(true, key);
                return;
            }

            ErrorMessage = result.ErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
