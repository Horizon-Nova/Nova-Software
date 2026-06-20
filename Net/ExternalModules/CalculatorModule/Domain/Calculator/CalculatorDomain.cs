using System.Globalization;

namespace ExternalModules.CalculatorModule.Domain.Calculator;

internal sealed class CalculatorDomain
{
    private double _accumulator;
    private double _pendingOperand;
    private string _pendingOperator = string.Empty;
    private bool _resetInputOnNextDigit;
    private string _currentInput = "0";
    private int _decimalPlaces = 8;

    public string CurrentDisplay => _currentInput;

    public void ApplyDecimalPlaces(int decimalPlaces)
    {
        _decimalPlaces = Math.Clamp(decimalPlaces, 0, 15);
    }

    public void ApplyDigit(string digit)
    {
        if (_resetInputOnNextDigit)
        {
            _currentInput = digit;
            _resetInputOnNextDigit = false;
            return;
        }

        _currentInput = _currentInput == "0" ? digit : _currentInput + digit;
    }

    public void ApplyDecimalSeparator()
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

    public void ApplyOperator(string operatorSymbol)
    {
        var currentValue = QueryCurrentInputValue();

        if (!string.IsNullOrEmpty(_pendingOperator))
        {
            _accumulator = Calculate(_accumulator, currentValue, _pendingOperator);
        }
        else
        {
            _accumulator = currentValue;
        }

        _pendingOperator = operatorSymbol;
        _currentInput = BuildResultText(_accumulator);
        _resetInputOnNextDigit = true;
    }

    public string CalculateExpression()
    {
        var currentValue = QueryCurrentInputValue();

        if (string.IsNullOrEmpty(_pendingOperator))
        {
            return _currentInput;
        }

        var result = Calculate(_accumulator, currentValue, _pendingOperator);
        _pendingOperand = currentValue;
        var expression = $"{BuildResultText(_accumulator)} {_pendingOperator} {BuildResultText(currentValue)} =";
        _accumulator = result;
        _currentInput = BuildResultText(result);
        _pendingOperator = string.Empty;
        _resetInputOnNextDigit = true;
        return expression;
    }

    public void ApplyClear()
    {
        _accumulator = 0;
        _pendingOperand = 0;
        _pendingOperator = string.Empty;
        _currentInput = "0";
        _resetInputOnNextDigit = false;
    }

    public void ApplyClearEntry()
    {
        _currentInput = "0";
    }

    public void ApplyBackspace()
    {
        if (_resetInputOnNextDigit)
        {
            return;
        }

        _currentInput = _currentInput.Length > 1
            ? _currentInput[..^1]
            : "0";
    }

    public void ApplySignToggle()
    {
        var value = QueryCurrentInputValue();
        _currentInput = BuildResultText(-value);
    }

    public void ApplyPercent()
    {
        var value = QueryCurrentInputValue();
        _currentInput = BuildResultText(value / 100.0);
    }

    private double QueryCurrentInputValue() =>
        double.TryParse(_currentInput, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private string BuildResultText(double value)
    {
        if (double.IsInfinity(value) || double.IsNaN(value))
        {
            return "錯誤";
        }

        var rounded = Math.Round(value, _decimalPlaces, MidpointRounding.AwayFromZero);
        return rounded.ToString("G", CultureInfo.InvariantCulture);
    }

    private static double Calculate(double left, double right, string operatorSymbol) =>
        operatorSymbol switch
        {
            "+" => left + right,
            "-" => left - right,
            "×" => left * right,
            "÷" => right == 0 ? double.NaN : left / right,
            _ => right
        };
}
