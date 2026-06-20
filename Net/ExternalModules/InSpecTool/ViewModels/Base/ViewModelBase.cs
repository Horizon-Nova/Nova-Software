using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExternalModules.InSpecTool.ViewModels.Base;

/// <summary>
/// 提供 WPF ViewModel 使用的屬性變更通知基底類別。
/// </summary>
internal abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>
    /// 當可繫結屬性值變更時引發，供 WPF 更新 UI。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 以目前成員名稱引發屬性變更通知。
    /// </summary>
    /// <param name="propertyName">欲通知的屬性名稱。</param>
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// 設定欄位值並在值變更時引發通知。
    /// </summary>
    /// <typeparam name="T">屬性值型別。</typeparam>
    /// <param name="storage">要寫入的欄位參考。</param>
    /// <param name="value">新的屬性值。</param>
    /// <param name="propertyName">欲通知的屬性名稱。</param>
    /// <returns>若值有變更則回傳 <c>true</c>。</returns>
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}

