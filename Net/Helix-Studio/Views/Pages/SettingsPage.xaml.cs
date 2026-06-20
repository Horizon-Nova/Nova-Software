using System.Windows;
using System.Windows.Controls;

namespace Helix_Studio.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage() => InitializeComponent();

    private void SettingsNav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;
        var target = tag switch
        {
            "General"    => (FrameworkElement?)SectionGeneral,
            "Display"    => SectionDisplay,
            "Workspaces" => SectionWorkspaces,
            "Log"        => SectionLog,
            "Alerts"     => SectionAlerts,
            "About"      => SectionAbout,
            _            => null,
        };
        target?.BringIntoView();
    }
}
