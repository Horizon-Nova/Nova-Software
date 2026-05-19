using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Helix_Studio.ViewModels.Base;

/// <summary>
/// ViewModel 基底類別。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 設定欄位值並在變更時通知 UI。
    /// </summary>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// 觸發屬性變更通知。
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// 觸發屬性變更通知（相容於舊用法）。
    /// </summary>
    protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        => OnPropertyChanged(propertyName);

    /// <summary>
    /// 設定欄位並通知變更（與 <see cref="SetProperty{T}"/> 相同）。
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        => SetProperty(ref field, value, propertyName);
}
