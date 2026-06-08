using System.Windows;
using System.Windows.Controls;

namespace ExternalModules.InSpecTool.Views.Dialogs;

internal sealed class InSpecLicensePromptDialog : Window
{
    private readonly PasswordBox _passwordBox;

    private InSpecLicensePromptDialog(string statusText)
    {
        Title = "模組授權";
        Width = 520;
        Height = 240;
        MinWidth = 460;
        MinHeight = 220;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Topmost = true;
        SetResourceReference(BackgroundProperty, "SurfaceRaised");

        var rootGrid = new Grid
        {
            Margin = new Thickness(20),
        };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = "此模組需要有效的模組金鑰",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
        };
        titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
        Grid.SetRow(titleBlock, 0);
        rootGrid.Children.Add(titleBlock);

        var descriptionBlock = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),
            Text = "系統已在本次啟動驗證已保存的模組金鑰。若未通過，請在此輸入正確金鑰完成驗證。",
            TextWrapping = TextWrapping.Wrap,
        };
        descriptionBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiary");
        Grid.SetRow(descriptionBlock, 1);
        rootGrid.Children.Add(descriptionBlock);

        var statusBlock = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),
            Text = statusText,
            TextWrapping = TextWrapping.Wrap,
        };
        statusBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiary");
        Grid.SetRow(statusBlock, 2);
        rootGrid.Children.Add(statusBlock);

        _passwordBox = new PasswordBox
        {
            Margin = new Thickness(0, 14, 0, 0),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _passwordBox.SetResourceReference(BorderBrushProperty, "BorderMedium");
        Grid.SetRow(_passwordBox, 3);
        rootGrid.Children.Add(_passwordBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };

        var cancelButton = new Button
        {
            Content = "取消",
            MinWidth = 82,
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(0, 0, 8, 0),
        };
        cancelButton.SetResourceReference(StyleProperty, "GhostButton");
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        var confirmButton = new Button
        {
            Content = "驗證",
            MinWidth = 82,
            Padding = new Thickness(14, 7, 14, 7),
            IsDefault = true,
        };
        confirmButton.SetResourceReference(StyleProperty, "PrimaryButton");
        confirmButton.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(confirmButton);
        Grid.SetRow(buttonPanel, 4);
        rootGrid.Children.Add(buttonPanel);

        Content = rootGrid;
        Loaded += (_, _) => _passwordBox.Focus();
    }

    public static string? Show(Window? owner, string statusText)
    {
        var dialog = new InSpecLicensePromptDialog(statusText);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true ? dialog._passwordBox.Password.Trim() : null;
    }
}
