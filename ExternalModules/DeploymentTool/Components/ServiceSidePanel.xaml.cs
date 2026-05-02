using System;
using System.Windows;
using System.Windows.Controls;

namespace ExternalModules.DeploymentTool.Components;

public partial class ServiceSidePanel : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(ServiceSidePanel),
            new PropertyMetadata("Horizon_Nova"));

    public static readonly DependencyProperty SelectedTabProperty =
        DependencyProperty.Register(
            nameof(SelectedTab),
            typeof(string),
            typeof(ServiceSidePanel),
            new PropertyMetadata("Deployments"));

    public ServiceSidePanel()
    {
        InitializeComponent();
    }

    public event EventHandler? CloseClicked;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string SelectedTab
    {
        get => (string)GetValue(SelectedTabProperty);
        set => SetValue(SelectedTabProperty, value);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseClicked?.Invoke(this, EventArgs.Empty);
    }
}