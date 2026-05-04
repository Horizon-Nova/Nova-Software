using System.ComponentModel;

namespace ExternalModules.InSpecTool.Core;

public sealed class InSpecSettingsFieldRow : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _keyword = string.Empty;
    private string _targetMode = InSpecFieldDefinition.DirectInputTargetMode;
    private bool _isEditable = true;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public string Keyword
    {
        get => _keyword;
        set
        {
            if (_keyword == value)
            {
                return;
            }

            _keyword = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Keyword)));
        }
    }

    public string TargetMode
    {
        get => _targetMode;
        set
        {
            if (_targetMode == value)
            {
                return;
            }

            _targetMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetMode)));
        }
    }

    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            if (_isEditable == value)
            {
                return;
            }

            _isEditable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditable)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
