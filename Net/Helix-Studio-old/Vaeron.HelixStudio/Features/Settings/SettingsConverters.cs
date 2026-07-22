using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Vaeron.HelixStudio.Features.WorkspaceManagement.Contracts;

namespace Vaeron.HelixStudio.Features.Settings;

/// <summary>工作區列 Id 比對（編輯／刪除狀態），用於 Visibility 綁定</summary>
public sealed class WorkspaceRowMatchesConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [int rowId, WorkspaceSnapshot target] && rowId == target.Id ? Visibility.Visible : Visibility.Collapsed;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>工作區是否為目前使用中，用於 Visibility 綁定</summary>
public sealed class WorkspaceIsActiveConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [int id, int active] && id == active ? Visibility.Visible : Visibility.Collapsed;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>工作區列是否處於編輯或刪除確認狀態，用於 Table 展開列</summary>
public sealed class WorkspaceRowExpandedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [int rowId, _, _]) return Visibility.Collapsed;
        var editing = values[1] as WorkspaceSnapshot;
        var deleting = values[2] as WorkspaceSnapshot;
        return editing?.Id == rowId || deleting?.Id == rowId ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>刪除確認輸入是否匹配名稱</summary>
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

/// <summary>記錄保留期限下拉選項顯示文字</summary>
public sealed class RetentionDaysToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int days ? (days <= 0 ? "永久保留" : $"{days} 天") : value ?? "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>睡眠／逾時分鐘數下拉選項顯示文字（0 代表從不）</summary>
public sealed class SleepMinutesToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int minutes ? (minutes <= 0 ? "從不" : $"{minutes} 分鐘") : value ?? "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>閒置鎖定分鐘數下拉選項顯示文字（0 代表不鎖定）</summary>
public sealed class IdleLockMinutesToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int minutes ? (minutes <= 0 ? "不鎖定" : $"{minutes} 分鐘") : value ?? "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
