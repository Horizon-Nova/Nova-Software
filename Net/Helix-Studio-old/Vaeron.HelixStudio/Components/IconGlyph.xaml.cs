using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Vaeron.HelixStudio.Components;

public partial class IconGlyph : UserControl
{
    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.Register(nameof(IconData), typeof(Geometry), typeof(IconGlyph));

    public Geometry? IconData
    {
        get => (Geometry?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public IconGlyph()
    {
        InitializeComponent();
    }
}
