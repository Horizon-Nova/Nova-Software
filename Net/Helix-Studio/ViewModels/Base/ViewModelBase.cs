using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace Helix_Studio.ViewModels.Base;

/// <summary>實作 <see cref="INotifyPropertyChanged"/> 的 ViewModel 基底，提供屬性變更通知輔助方法。</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>當屬性值變更時引發，供 WPF 繫結更新 UI。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>以 <paramref name="propertyName"/> 觸發屬性變更；未指定則由呼叫端成員名稱推斷。</summary>
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>當 <paramref name="value"/> 與目前值不同時寫入 <paramref name="storage"/> 並觸發屬性變更；否則回傳 <c>false</c>。</summary>
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}

