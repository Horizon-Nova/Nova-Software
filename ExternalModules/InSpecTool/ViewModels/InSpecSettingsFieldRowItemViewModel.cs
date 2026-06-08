using ExternalModules.InSpecTool.Model;
using ExternalModules.InSpecTool.ViewModels.Base;

namespace ExternalModules.InSpecTool.ViewModels;

internal sealed class InSpecSettingsFieldRowItemViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _keyword = string.Empty;
    private string _targetMode = InSpecFieldDefinitionModel.DirectInputTargetMode;
    private bool _isEditable = true;
    private bool _isLocked;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    public string Keyword
    {
        get => _keyword;
        set => SetProperty(ref _keyword, value ?? string.Empty);
    }

    public string TargetMode
    {
        get => _targetMode;
        set => SetProperty(ref _targetMode, value ?? string.Empty);
    }

    public bool IsEditable
    {
        get => _isEditable;
        set => SetProperty(ref _isEditable, value);
    }

    public bool IsLocked
    {
        get => _isLocked;
        set => SetProperty(ref _isLocked, value);
    }

    public InSpecSettingsFieldRowModel BuildModel() =>
        new()
        {
            Name = Name,
            Keyword = Keyword,
            TargetMode = TargetMode,
            IsEditable = IsEditable,
            IsLocked = IsLocked
        };

    public static InSpecSettingsFieldRowItemViewModel Create(InSpecSettingsFieldRowModel model) =>
        new()
        {
            Name = model.Name,
            Keyword = model.Keyword,
            TargetMode = model.TargetMode,
            IsEditable = model.IsEditable,
            IsLocked = model.IsLocked
        };
}
