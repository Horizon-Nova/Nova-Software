using System;
using System.ComponentModel;

namespace HorizonNova.SampleModule.ViewModels;

public sealed class SampleModulePageViewModel : INotifyPropertyChanged
{
    private string _workspaceText = "No active workspace.";

    public string WorkspaceText
    {
        get => _workspaceText;
        private set
        {
            if (string.Equals(_workspaceText, value, StringComparison.Ordinal))
            {
                return;
            }

            _workspaceText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WorkspaceText)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyWorkspace(string? workspaceName)
    {
        WorkspaceText = string.IsNullOrWhiteSpace(workspaceName)
            ? "No active workspace."
            : $"Active workspace: {workspaceName}";
    }
}
