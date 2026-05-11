using System.Windows;

namespace Helix_Studio.Views;

/// <summary>
/// 模組授權輸入對話框。
/// </summary>
public partial class ModuleLicenseDialog : Window
{
    /// <summary>
    /// 建立模組授權輸入對話框。
    /// </summary>
    /// <param name="moduleId">模組識別值。</param>
    /// <param name="displayName">模組顯示名稱。</param>
    /// <param name="initialError">初始錯誤訊息。</param>
    public ModuleLicenseDialog(string moduleId, string displayName, string initialError = "")
    {
        InitializeComponent();
    }
}
