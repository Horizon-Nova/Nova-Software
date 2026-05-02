using System.Windows;
using System.Windows.Controls;
using ExternalModules.CalculatorModule.Core;
using HN_Studio.ModuleSdk;

namespace ExternalModules.CalculatorModule.Views;

public partial class CalculatorView : UserControl
{
    private HnModuleContext? _hostContext;
    private readonly CalculatorEngine _engine = new();

    public CalculatorView()
    {
        InitializeComponent();
        SyncDisplay();
    }

    internal void ApplyHostContext(HnModuleContext context)
    {
        _hostContext = context;

        var decimalPlaces = context.Settings.QueryInt32("decimalPlaces", 8);
        _engine.SetDecimalPlaces(decimalPlaces);

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
            HistoryColumn.Width = new GridLength(160);
            HistoryPanel.Visibility = Visibility.Visible;
        }
        else
        {
            HistoryColumn.Width = new GridLength(0);
            HistoryPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void DigitButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button button)
        {
            return;
        }

        _engine.InputDigit(button.Content?.ToString() ?? "0");
        SyncDisplay();
    }

    private void DecimalButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        _engine.InputDecimal();
        SyncDisplay();
    }

    private void OperatorButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button button)
        {
            return;
        }

        var operatorSymbol = button.Content?.ToString() ?? "+";

        if (operatorSymbol == "÷")
        {
            operatorSymbol = "÷";
        }

        _engine.InputOperator(operatorSymbol);
        SyncDisplay();
    }

    private void EqualsButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        var expression = _engine.Evaluate();
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

        switch (button.Content?.ToString())
        {
            case "C":
                _engine.Clear();
                ExpressionText.Text = string.Empty;
                break;
            case "CE":
                _engine.ClearEntry();
                break;
            case "⌫":
                _engine.Backspace();
                break;
            case "±":
                _engine.ToggleSign();
                break;
            case "%":
                _engine.Percent();
                break;
        }

        SyncDisplay();
    }

    private void SyncDisplay()
    {
        DisplayText.Text = _engine.CurrentDisplay;
    }
}
