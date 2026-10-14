using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Vaeron.VNCSCP.Internal.Wpf.Components;

/// <summary>可被整組勾選的清單列。群組標頭的全選狀態由此介面讀取。</summary>
public interface ISelectableRow
{
    public bool IsSelected { get; set; }
}

/// <summary>反轉布林。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is false;
}

/// <summary>布林轉可見性。轉換本身用平台原生的 BooleanToVisibilityConverter。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    private static readonly BooleanToVisibilityConverter Native = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Native.Convert(value, targetType, parameter, culture);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Native.ConvertBack(value, targetType, parameter, culture);
}

/// <summary>布林反轉後轉可見性。</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    private static readonly BooleanToVisibilityConverter Native = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Native.Convert(value is false, targetType, parameter, culture);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>字串非空白時可見。</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    private static readonly BooleanToVisibilityConverter Native = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Native.Convert(string.IsNullOrWhiteSpace(value as string) is false, targetType, parameter, culture);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>字串等於參數。供一組選項共用同一個字串來源，選中的那個回寫自己的參數值。</summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Equals(value as string, parameter as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new[] { parameter }
            .Where(_ => value is true)
            .DefaultIfEmpty(Binding.DoNothing)
            .First()!;
}

/// <summary>群組底下的列是否全被勾選。</summary>
public sealed class GroupAllSelectedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new[] { value }
            .OfType<CollectionViewGroup>()
            .SelectMany(group => group.Items.OfType<ISelectableRow>())
            .All(row => row.IsSelected);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
