using System.Windows;
using Helix_Studio.ViewModels;

namespace Helix_Studio.Views;

/// <summary>模組需要授權金鑰時顯示的驗證對話框；關閉後可讀取 <see cref="LicenseKey"/>。</summary>
public partial class ModuleLicenseDialog : Window
{
    /// <summary>使用者確認後通過驗證的授權金鑰；取消或關閉則不更新。</summary>
    public string LicenseKey { get; private set; } = string.Empty;

    /// <summary>建立對話框並以指定模組顯示名稱與可選的初始錯誤訊息。</summary>
    public ModuleLicenseDialog(string moduleId, string displayName, string initialError = "")
    {
        InitializeComponent();

        var vm = new ModuleLicenseDialogViewModel(moduleId, displayName, initialError);
        vm.CloseRequested += OnCloseRequested;
        DataContext = vm;
    }

    private void OnCloseRequested(bool? dialogResult, string? licenseKey)
    {
        if (DataContext is ModuleLicenseDialogViewModel vm)
            vm.CloseRequested -= OnCloseRequested;

        if (dialogResult == true && !string.IsNullOrWhiteSpace(licenseKey))
            LicenseKey = licenseKey.Trim();

        DialogResult = dialogResult;
        Close();
    }
}
