using Helix_Studio.Mvvm;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 所有頁面 ViewModel 的基底。提供導覽識別與標題，供 Shell 與 DataTemplate 使用。
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    /// <summary>導覽鍵，對應側邊欄項目，唯一識別此頁。</summary>
    public abstract string PageKey { get; }
}
