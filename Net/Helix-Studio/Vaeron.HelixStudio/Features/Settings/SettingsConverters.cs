using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Vaeron.HelixStudio.Features.WorkspaceManagement.Contracts;

namespace Vaeron.HelixStudio.Features.Settings;

/// <summary>分類側邊導覽 pill 的選取視覺——比對這個分類鍵跟目前選取的分類鍵，兩者分屬不同
/// DataContext，只能用 MultiBinding。獨立於 EventLog 的同類轉換器，不跨 Feature 共用同一個
/// 類別（solution-structure.md 依賴法：Feature 之間不得互相引用，即使邏輯重複幾行也一樣）。</summary>
public sealed class CategoryKeyToButtonStyleConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [string key, string current] && key == current
            ? Application.Current.TryFindResource("PrimaryButtonStyle")
            : Application.Current.TryFindResource("SecondaryButtonStyle");

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>比對這一列工作區的 Id 跟目前正在編輯／要求刪除的工作區 Id——用於決定該列要不要
/// 顯示編輯表單或刪除確認 popup。</summary>
public sealed class WorkspaceRowMatchesConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [int rowId, WorkspaceSnapshot target] && rowId == target.Id;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>刪除確認輸入框是否已打對名稱——「輸入完整名稱才能刪除」的高摩擦力確認，
/// 打對之前確認按鈕維持停用。</summary>
public sealed class DeleteConfirmEnabledConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [string typed, WorkspaceSnapshot target] && typed.Length > 0 && typed == target.Name;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class CountGreaterThanOneConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count > 1;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class CheckUpdateLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "檢查中…" : "檢查更新";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
