using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExternalModules.InSpecTool.Core;

public sealed class PreviewFieldItem : INotifyPropertyChanged
{
    private string _value = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Value
    {
        get => _value;
        set
        {
            var normalizedValue = value ?? string.Empty;
            if (string.Equals(_value, normalizedValue, StringComparison.Ordinal))
            {
                return;
            }

            _value = normalizedValue;
            OnPropertyChanged();
        }
    }

    public bool IsReadOnly { get; init; }

    public string Keyword { get; init; } = string.Empty;

    public string TargetMode { get; init; } = InSpecFieldDefinition.DirectInputTargetMode;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool QueryRequiresConfirmation() =>
        string.Equals(
            InSpecFieldDefinition.NormalizeTargetMode(TargetMode),
            InSpecFieldDefinition.PromptConfirmationTargetMode,
            StringComparison.Ordinal);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
