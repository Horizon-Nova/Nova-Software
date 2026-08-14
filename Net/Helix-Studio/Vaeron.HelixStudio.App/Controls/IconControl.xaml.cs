using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Vaeron.HelixStudio.App.Controls;

public partial class IconControl : UserControl
{
    public static readonly DependencyProperty IconNameProperty = DependencyProperty.Register(
        nameof(IconName), typeof(string), typeof(IconControl), new PropertyMetadata("", OnIconNameChanged));

    public IconControl() => InitializeComponent();

    public string IconName
    {
        get => (string)GetValue(IconNameProperty);
        set => SetValue(IconNameProperty, value);
    }

    private static void OnIconNameChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var control = (IconControl)target;
        control.StrokeShape.Data = control.TryFindResource($"Icon.{args.NewValue}.Stroke") as Geometry;
        control.FillShape.Data = control.TryFindResource($"Icon.{args.NewValue}.Fill") as Geometry;
    }
}
