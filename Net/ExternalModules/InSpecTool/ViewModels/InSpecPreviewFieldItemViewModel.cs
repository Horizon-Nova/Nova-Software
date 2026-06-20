using ExternalModules.InSpecTool.Model;
using ExternalModules.InSpecTool.ViewModels.Base;

namespace ExternalModules.InSpecTool.ViewModels;

internal sealed class InSpecPreviewFieldItemViewModel : ViewModelBase
{
    private string _value = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value ?? string.Empty);
    }

    public bool IsReadOnly { get; init; }

    public string Keyword { get; init; } = string.Empty;

    public string TargetMode { get; init; } = InSpecFieldDefinitionModel.DirectInputTargetMode;

    public InSpecPreviewFieldModel BuildModel() =>
        new()
        {
            Label = Label,
            Value = Value,
            IsReadOnly = IsReadOnly,
            Keyword = Keyword,
            TargetMode = TargetMode
        };

    public static InSpecPreviewFieldItemViewModel Create(InSpecPreviewFieldModel model) =>
        new()
        {
            Label = model.Label,
            Value = model.Value,
            IsReadOnly = model.IsReadOnly,
            Keyword = model.Keyword,
            TargetMode = model.TargetMode
        };
}
