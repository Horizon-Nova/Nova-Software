using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.Components;

public partial class ProcessGatedBanner : UserControl
{
    public static readonly DependencyProperty ShowBannerProperty =
        DependencyProperty.Register(nameof(ShowBanner), typeof(bool), typeof(ProcessGatedBanner));

    public static readonly DependencyProperty BannerTextProperty =
        DependencyProperty.Register(nameof(BannerText), typeof(string), typeof(ProcessGatedBanner));

    public bool ShowBanner
    {
        get => (bool)GetValue(ShowBannerProperty);
        set => SetValue(ShowBannerProperty, value);
    }

    public string BannerText
    {
        get => (string)GetValue(BannerTextProperty);
        set => SetValue(BannerTextProperty, value);
    }

    public ProcessGatedBanner() => InitializeComponent();
}
