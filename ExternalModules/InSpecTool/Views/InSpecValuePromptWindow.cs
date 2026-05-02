using System.Windows;
using System.Windows.Controls;

namespace ExternalModules.InSpecTool.Views;

public sealed class InSpecValuePromptWindow : Window
{
    private readonly TextBox _valueTextBox;

    private InSpecValuePromptWindow(string inputTypeName, string defaultValue)
    {
        Title = "輸入欄位值";
        Width = 480;
        Height = 210;
        MinWidth = 420;
        MinHeight = 200;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Topmost = true;
        SetResourceReference(BackgroundProperty, "SurfaceRaised");

        var root = new Grid
        {
            Margin = new Thickness(20)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = "請輸入需要手動確認的欄位值",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold
        };
        titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
        Grid.SetRow(titleBlock, 0);
        root.Children.Add(titleBlock);

        var descriptionBlock = new TextBlock
        {
            Text = $"欄位：{inputTypeName}",
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        descriptionBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiary");
        Grid.SetRow(descriptionBlock, 1);
        root.Children.Add(descriptionBlock);

        _valueTextBox = new TextBox
        {
            Margin       = new Thickness(0, 14, 0, 0),
            Padding      = new Thickness(8, 6, 8, 6),
            FontSize     = 13,
            VerticalAlignment = VerticalAlignment.Top,
            Text         = defaultValue ?? string.Empty
        };
        _valueTextBox.SetResourceReference(TextBox.BorderBrushProperty, "BorderMedium");
        Grid.SetRow(_valueTextBox, 2);
        root.Children.Add(_valueTextBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancelButton = new Button
        {
            Content  = "取消",
            MinWidth = 82,
            Padding  = new Thickness(14, 7, 14, 7),
            Margin   = new Thickness(0, 0, 8, 0)
        };
        cancelButton.SetResourceReference(StyleProperty, "GhostButton");
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        var confirmButton = new Button
        {
            Content   = "確認",
            MinWidth  = 82,
            Padding   = new Thickness(14, 7, 14, 7),
            IsDefault = true
        };
        confirmButton.SetResourceReference(StyleProperty, "PrimaryButton");
        confirmButton.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(confirmButton);
        Grid.SetRow(buttonPanel, 3);
        root.Children.Add(buttonPanel);

        Content = root;
        Loaded += (_, _) =>
        {
            _valueTextBox.Focus();
            _valueTextBox.SelectAll();
        };
    }

    public static string? Show(Window? owner, string inputTypeName, string defaultValue)
    {
        var window = new InSpecValuePromptWindow(inputTypeName, defaultValue);
        if (owner is not null)
        {
            window.Owner = owner;
        }

        return window.ShowDialog() == true
            ? window._valueTextBox.Text.Trim()
            : null;
    }
}
