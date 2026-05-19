using System.Windows;

namespace Helix_Studio.Views.Converters;

/// <summary>
/// DynamicResource Proxy。
///
/// 用途：
/// - WPF 不允許在 <see cref="System.Windows.Data.Binding.Source"/> 直接使用 <c>{DynamicResource ...}</c>。
/// - 透過 <see cref="Freezable"/> + <see cref="DependencyProperty"/> 承載 DynamicResource，
///   讓 MultiBinding 可以以一般 Binding 方式讀取資源值，並在資源切換時自然更新。
/// </summary>
public sealed class DynamicResourceProxy : Freezable
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(object),
            typeof(DynamicResourceProxy),
            new PropertyMetadata(null));

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new DynamicResourceProxy();
}

