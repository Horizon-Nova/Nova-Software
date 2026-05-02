using System.Globalization;

namespace ExternalModules.CalculatorModule.Core;

internal sealed class CalculatorEngine
{
    private double _accumulator;
    private double _pendingOperand;
    private string _pendingOperator = string.Empty;
    private bool _resetInputOnNextDigit;
    private string _currentInput = "0";
    private int _decimalPlaces = 8;

    public string CurrentDisplay => _currentInput;

    public void SetDecimalPlaces(int decimalPlaces)
    {
        _decimalPlaces = Math.Clamp(decimalPlaces, 0, 15);
    }

    public void InputDigit(string digit)
    {
        if (_resetInputOnNextDigit)
        {
            _currentInput = digit;
            _resetInputOnNextDigit = false;
            return;
        }

        _currentInput = _currentInput == "0" ? digit : _currentInput + digit;
    }

    public void InputDecimal()
    {
        if (_resetInputOnNextDigit)
        {
            _currentInput = "0.";
            _resetInputOnNextDigit = false;
            return;
        }

        if (!_currentInput.Contains('.'))
        {
            _currentInput += ".";
        }
    }

    public void InputOperator(string operatorSymbol)
    {
        var currentValue = ParseCurrentInput();

        if (!string.IsNullOrEmpty(_pendingOperator))
        {
            _accumulator = Evaluate(_accumulator, currentValue, _pendingOperator);
        }
        else
        {
            _accumulator = currentValue;
        }

        _pendingOperator = operatorSymbol;
        _currentInput = FormatResult(_accumulator);
        _resetInputOnNextDigit = true;
    }

    public string Evaluate()
    {
        var currentValue = ParseCurrentInput();

        if (string.IsNullOrEmpty(_pendingOperator))
        {
            return _currentInput;
        }

        var result = Evaluate(_accumulator, currentValue, _pendingOperator);
        _pendingOperand = currentValue;
        var expression = $"{FormatResult(_accumulator)} {_pendingOperator} {FormatResult(currentValue)} =";
        _accumulator = result;
        _currentInput = FormatResult(result);
        _pendingOperator = string.Empty;
        _resetInputOnNextDigit = true;
        return expression;
    }

    public void Clear()
    {
        _accumulator = 0;
        _pendingOperand = 0;
        _pendingOperator = string.Empty;
        _currentInput = "0";
        _resetInputOnNextDigit = false;
    }

    public void ClearEntry()
    {
        _currentInput = "0";
    }

    public void Backspace()
    {
        if (_resetInputOnNextDigit)
        {
            return;
        }

        _currentInput = _currentInput.Length > 1
            ? _currentInput[..^1]
            : "0";
    }

    public void ToggleSign()
    {
        var value = ParseCurrentInput();
        _currentInput = FormatResult(-value);
    }

    public void Percent()
    {
        var value = ParseCurrentInput();
        _currentInput = FormatResult(value / 100.0);
    }

    private double ParseCurrentInput() =>
        double.TryParse(_currentInput, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private string FormatResult(double value)
    {
        if (double.IsInfinity(value) || double.IsNaN(value))
        {
            return "錯誤";
        }

        var rounded = Math.Round(value, _decimalPlaces, MidpointRounding.AwayFromZero);
        return rounded.ToString("G", CultureInfo.InvariantCulture);
    }

    private static double Evaluate(double left, double right, string operatorSymbol) =>
        operatorSymbol switch
        {
            "+" => left + right,
            "-" => left - right,
            "×" => left * right,
            "÷" => right == 0 ? double.NaN : left / right,
            _ => right
        };
}
