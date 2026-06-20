using System.Windows.Input;
using Helix_Studio.Models;
using Helix_Studio.Mvvm;

namespace Helix_Studio.ViewModels;

/// <summary>
/// 工具宿主頁：承載一個執行中工具的嵌入視窗。
/// </summary>
public sealed class ToolHostViewModel : ViewModelBase
{
    private string _toolName = string.Empty;
    private nint _windowHandle;

    public override string PageKey => "toolhost";

    public InstalledTool? Current { get; private set; }

    public string ToolName
    {
        get => _toolName;
        private set => SetProperty(ref _toolName, value);
    }

    /// <summary>嵌入的工具主視窗 HWND；0 表示無視窗（headless）。</summary>
    public nint WindowHandle
    {
        get => _windowHandle;
        private set => SetProperty(ref _windowHandle, value);
    }

    public ICommand StopCommand { get; }

    public event Action<InstalledTool>? StopRequested;

    public ToolHostViewModel()
    {
        StopCommand = new RelayCommand(() =>
        {
            if (Current is not null) StopRequested?.Invoke(Current);
        });
    }

    public void SetTool(InstalledTool? tool, nint windowHandle)
    {
        Current = tool;
        ToolName = tool?.Name ?? string.Empty;
        WindowHandle = windowHandle;
    }
}
