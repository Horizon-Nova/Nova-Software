using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Helix_Studio.Mvvm;

/// <summary>
/// 可觀察物件基底：實作 INotifyPropertyChanged，提供 SetProperty 通用更新邏輯。
/// ViewModel 與會即時變動欄位的 Model 共用此基底。
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>值有變才更新並通知；回傳是否實際變動。</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
