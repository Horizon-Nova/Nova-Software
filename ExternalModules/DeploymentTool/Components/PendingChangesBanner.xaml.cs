using System;
using System.Windows;
using System.Windows.Controls;

namespace ExternalModules.DeploymentTool.Components;

public partial class PendingChangesBanner : UserControl
{
    public static readonly DependencyProperty TitleTextProperty =
        DependencyProperty.Register(
            nameof(TitleText),
            typeof(string),
            typeof(PendingChangesBanner),
            new PropertyMetadata(string.Empty));

    public PendingChangesBanner()
    {
        InitializeComponent();
    }

    public event EventHandler? DetailsClicked;
    public event EventHandler? DeployClicked;
    public event EventHandler? DiscardClicked;

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    private void DetailsButton_OnClick(object sender, RoutedEventArgs e)
    {
        DetailsClicked?.Invoke(this, EventArgs.Empty);
    }

    private void DeployButton_OnClick(object sender, RoutedEventArgs e)
    {
        DeployClicked?.Invoke(this, EventArgs.Empty);
    }

    private void MoreButton_OnClick(object sender, RoutedEventArgs e)
    {
        bool isPopupOpen = MoreActionsPopup.IsOpen;
        MoreActionsPopup.IsOpen = isPopupOpen ? false : true;
        e.Handled = true;
    }

    private void DiscardMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        MoreActionsPopup.IsOpen = false;
        DiscardClicked?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}