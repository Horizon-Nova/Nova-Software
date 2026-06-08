using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ExternalModules.CalculatorModule.Domain.Calculator;
using HorizonNova.HelixStudio.ModuleSdk;

namespace ExternalModules.CalculatorModule.Views;

public partial class CalculatorView : UserControl
{
    private static readonly Dictionary<string, Action<CalculatorDomain>> FunctionActions = new()
    {
        ["C"]  = d => d.ApplyClear(),
        ["CE"] = d => d.ApplyClearEntry(),
        ["⌫"]  = d => d.ApplyBackspace(),
        ["±"]  = d => d.ApplySignToggle(),
        ["%"]  = d => d.ApplyPercent(),
    };

    private IShellContext? _hostContext;
    private readonly CalculatorDomain _domain = new();

    public CalculatorView()
    {
        InitializeComponent();
        SyncDisplay();
    }

    internal void ApplyHostContext(IShellContext context)
    {
        _hostContext = context;

        var decimalPlaces = context.Settings.QueryInt32("decimalPlaces", 8);
        _domain.ApplyDecimalPlaces(decimalPlaces);

        var showHistory = context.Settings.QueryBoolean("showHistory", true);
        ApplyHistoryVisibility(showHistory);
    }

    internal void ClearHostContext()
    {
        _hostContext = null;
    }

    private void ApplyHistoryVisibility(bool show)
    {
        if (show)
        {
            HistoryColumn.Width = new System.Windows.GridLength(160);
            HistoryPanel.Visibility = Visibility.Visible;
        }
        else
        {
            HistoryColumn.Width = new System.Windows.GridLength(0);
            HistoryPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void DigitButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button button)
        {
            return;
        }

        _domain.ApplyDigit(button.Content?.ToString() ?? "0");
        SyncDisplay();
    }

    private void DecimalButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        _domain.ApplyDecimalSeparator();
        SyncDisplay();
    }

    private void OperatorButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button button)
        {
            return;
        }

        _domain.ApplyOperator(button.Content?.ToString() ?? "+");
        SyncDisplay();
    }

    private void EqualsButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        var expression = _domain.CalculateExpression();
        SyncDisplay();
        ExpressionText.Text = expression;

        if (!string.IsNullOrWhiteSpace(expression))
        {
            HistoryListBox.Items.Insert(0, expression);
        }
    }

    private void FunctionButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button button)
        {
            return;
        }

        var key = button.Content?.ToString() ?? string.Empty;
        if (!FunctionActions.TryGetValue(key, out var action))
        {
            return;
        }

        action(_domain);
        SyncDisplay();

        if (key == "C")
        {
            ExpressionText.Text = string.Empty;
        }
    }

    private void SyncDisplay()
    {
        DisplayText.Text = _domain.CurrentDisplay;
    }
}
