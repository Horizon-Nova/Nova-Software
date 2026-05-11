using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Helix_Studio.ViewModels.Base;

/// <summary>ViewModel 基底。</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>屬性變更事件。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>通知屬性變更。</summary>
    /// <param name="propertyName">屬性名稱。</param>
    protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>設定欄位並通知變更。</summary>
    /// <typeparam name="T">欄位型別。</typeparam>
    /// <param name="field">欄位參考。</param>
    /// <param name="value">新值。</param>
    /// <param name="propertyName">屬性名稱。</param>
    /// <returns>是否有變更。</returns>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        NotifyPropertyChanged(propertyName);
        return true;
    }
}
